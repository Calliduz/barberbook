import datetime
import os
import re
from pymongo import MongoClient

MONGO_URI = (
    os.getenv("MONGODB_URI")
    or "mongodb+srv://cabunocjoshlisteristuris_db_user:QtjA8IW2dDSZkdVF@cluster0.ivvillv.mongodb.net/?appName=Cluster0"
)
DB_NAME = os.getenv("MONGODB_DB_NAME", "barberbook")

ISO_REGEX = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")

def normalize_value(val):
    if isinstance(val, str) and ISO_REGEX.match(val):
        try:
            dt = datetime.datetime.fromisoformat(val)
            if dt.tzinfo is not None:
                utc_dt = dt.astimezone(datetime.timezone.utc)
                # Formats as 2026-09-14T08:00:00Z (or with microseconds if non-zero)
                if utc_dt.microsecond > 0:
                    return utc_dt.strftime("%Y-%m-%dT%H:%M:%S.%fZ")
                else:
                    return utc_dt.strftime("%Y-%m-%dT%H:%M:%SZ")
        except Exception:
            pass
    elif isinstance(val, dict):
        return {k: normalize_value(v) for k, v in val.items()}
    elif isinstance(val, list):
        return [normalize_value(x) for x in val]
    return val

def main():
    print(f"Connecting to MongoDB: {DB_NAME}...")
    client = MongoClient(MONGO_URI)
    db = client[DB_NAME]
    
    total_updated = 0
    for col_name in db.list_collection_names():
        if col_name.startswith("system."):
            continue
        coll = db[col_name]
        updated_in_col = 0
        for doc in coll.find():
            _id = doc["_id"]
            new_doc = normalize_value(doc)
            if new_doc != doc:
                coll.replace_one({"_id": _id}, new_doc)
                updated_in_col += 1
        if updated_in_col > 0:
            print(f"Collection '{col_name}': normalized {updated_in_col} documents.")
            total_updated += updated_in_col

    print(f"Timestamp normalization complete. Total documents updated: {total_updated}")

if __name__ == "__main__":
    main()

