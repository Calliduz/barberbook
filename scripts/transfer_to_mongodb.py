#!/usr/bin/env python3
"""
BarberBook -> MongoDB Atlas Transfer Utility
Extracts all PostgreSQL database tables and project source files,
transforming and loading them into MongoDB Atlas collections.
"""

import os
import sys
import datetime
from decimal import Decimal
import psycopg2
from psycopg2.extras import RealDictCursor
import pymongo
from bson.decimal128 import Decimal128

MONGO_URI = os.environ.get(
    "MONGODB_URI",
    "mongodb+srv://cabunocjoshlisteristuris_db_user:QtjA8IW2dDSZkdVF@cluster0.ivvillv.mongodb.net/?appName=Cluster0"
)
DB_NAME = os.environ.get("MONGODB_DB_NAME", "barberbook")
PG_CONN_STRING = os.environ.get(
    "PG_CONN_STRING",
    "postgresql://barberbook:barberbook_dev_password@localhost:5432/barberbook_dev"
)

def serialize_value(val):
    """Convert PostgreSQL/Python types to BSON-friendly types."""
    if val is None:
        return None
    if isinstance(val, Decimal):
        # Store as Decimal128 for precise monetary value
        return Decimal128(str(val))
    if isinstance(val, (datetime.date, datetime.time)):
        return val.isoformat()
    if isinstance(val, datetime.datetime):
        return val
    if isinstance(val, bytes):
        return val.decode("utf-8", errors="replace")
    if isinstance(val, list):
        return [serialize_value(item) for item in val]
    if isinstance(val, dict):
        return {k: serialize_value(v) for k, v in val.items()}
    return val

def transfer_database_tables(pg_conn, mongo_db):
    print("=" * 60)
    print("1. TRANSFERRING POSTGRESQL TABLES TO MONGODB")
    print("=" * 60)

    cur = pg_conn.cursor(cursor_factory=RealDictCursor)
    cur.execute("""
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public' 
        ORDER BY table_name;
    """)
    tables = [r["table_name"] for r in cur.fetchall()]
    print(f"Discovered {len(tables)} PostgreSQL tables.")

    total_records = 0
    table_summary = {}

    for table in tables:
        cur.execute(f'SELECT * FROM "{table}";')
        rows = cur.fetchall()
        count = len(rows)
        total_records += count

        coll = mongo_db[table]
        # Drop existing records in collection to ensure fresh sync
        coll.delete_many({})

        if rows:
            documents = []
            for row in rows:
                doc = {k: serialize_value(v) for k, v in row.items()}
                # Set a friendly string _id if 'id' exists
                if "id" in doc:
                    doc["_pg_id"] = doc["id"]
                documents.append(doc)

            coll.insert_many(documents)
            print(f"  ✓ {table:<24}: {count:>4} rows inserted")
        else:
            print(f"  - {table:<24}:    0 rows (empty table)")

        table_summary[table] = count

    return table_summary, total_records

def transfer_project_files(repo_root, mongo_db):
    print("\n" + "=" * 60)
    print("2. TRANSFERRING PROJECT FILES TO MONGODB (project_files)")
    print("=" * 60)

    coll = mongo_db["project_files"]
    coll.delete_many({})

    # Include source code, documents, configs, and assets
    allowed_exts = {
        ".cs", ".cshtml", ".json", ".md", ".css", ".js", ".sql", ".xml",
        ".props", ".targets", ".yml", ".yaml", ".svg", ".png", ".jpg"
    }
    exclude_dirs = {
        ".git", ".vs", "bin", "obj", "node_modules", ".gemini", "TestResults"
    }

    file_docs = []
    total_size = 0

    for root, dirs, files in os.walk(repo_root):
        dirs[:] = [d for d in dirs if d not in exclude_dirs]
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            if ext not in allowed_exts and f not in ["Dockerfile", ".gitignore", "LICENSE"]:
                continue

            full_path = os.path.join(root, f)
            rel_path = os.path.relpath(full_path, repo_root).replace("\\", "/")
            stat = os.stat(full_path)
            size = stat.st_size
            total_size += size

            is_text = ext in {
                ".cs", ".cshtml", ".json", ".md", ".css", ".js", ".sql", ".xml",
                ".props", ".targets", ".yml", ".yaml", ".svg"
            } or f in ["Dockerfile", ".gitignore", "LICENSE"]

            doc = {
                "relativePath": rel_path,
                "fileName": f,
                "extension": ext,
                "sizeBytes": size,
                "lastModified": datetime.datetime.fromtimestamp(stat.st_mtime, tz=datetime.timezone.utc),
                "isText": is_text
            }

            # Embed text files directly in document
            if is_text and size < 500_000:
                try:
                    with open(full_path, "r", encoding="utf-8") as file_in:
                        doc["content"] = file_in.read()
                except Exception:
                    try:
                        with open(full_path, "r", encoding="latin-1") as file_in:
                            doc["content"] = file_in.read()
                    except Exception:
                        doc["content"] = None
            else:
                doc["content"] = None

            file_docs.append(doc)

    if file_docs:
        # Insert in chunks of 200
        for i in range(0, len(file_docs), 200):
            coll.insert_many(file_docs[i:i + 200])

    print(f"  ✓ project_files: {len(file_docs)} files transferred ({total_size / (1024 * 1024):.2f} MB)")
    return len(file_docs), total_size

def main():
    print("Connecting to PostgreSQL...")
    pg_conn = psycopg2.connect(PG_CONN_STRING)
    print("PostgreSQL connection successful.")

    print(f"Connecting to MongoDB Atlas ({MONGO_URI.split('@')[-1]})...")
    mongo_client = pymongo.MongoClient(MONGO_URI, serverSelectionTimeoutMS=10000)
    mongo_db = mongo_client[DB_NAME]
    print(f"MongoDB connection successful. Target database: '{DB_NAME}'")

    # 1. Transfer database tables
    table_summary, total_records = transfer_database_tables(pg_conn, mongo_db)

    # 2. Transfer project files
    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    file_count, total_bytes = transfer_project_files(repo_root, mongo_db)

    # 3. Create metadata record
    meta_coll = mongo_db["_migration_metadata"]
    meta_coll.delete_many({})
    meta_coll.insert_one({
        "project": "BarberBook",
        "description": "Barber Shop Management System - Data & Files Sync",
        "timestamp": datetime.datetime.now(datetime.timezone.utc),
        "sourceDatabase": "PostgreSQL 16 (barberbook_dev)",
        "targetDatabase": f"MongoDB Atlas ({DB_NAME})",
        "totalTablesTransferred": len(table_summary),
        "totalRecordsTransferred": total_records,
        "totalFilesTransferred": file_count,
        "totalFileBytes": total_bytes,
        "tables": table_summary
    })

    print("\n" + "=" * 60)
    print("TRANSFER COMPLETE SUMMARY")
    print("=" * 60)
    print(f"Target Database : {DB_NAME}")
    print(f"Total Collections : {len(mongo_db.list_collection_names())}")
    print(f"Total Records     : {total_records}")
    print(f"Total Files       : {file_count} ({total_bytes / (1024 * 1024):.2f} MB)")
    print("Collections in MongoDB Atlas:")
    for name in sorted(mongo_db.list_collection_names()):
        count = mongo_db[name].count_documents({})
        print(f"  - {name:<26}: {count} documents")
    print("=" * 60)

    pg_conn.close()
    mongo_client.close()

if __name__ == "__main__":
    main()

