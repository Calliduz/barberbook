import os
from pymongo import MongoClient

uri = "mongodb+srv://cabunocjoshlisteristuris_db_user:QtjA8IW2dDSZkdVF@cluster0.ivvillv.mongodb.net/?appName=Cluster0"
client = MongoClient(uri)
db = client["barberbook"]
counters = db["counters"]

for coll_name in sorted(db.list_collection_names()):
    if coll_name == "counters":
        continue
    coll = db[coll_name]
    # Find max integer _id
    docs = list(coll.find({"_id": {"$type": "int"}}).sort("_id", -1).limit(1))
    max_id = docs[0]["_id"] if docs else 0
    print(f"{coll_name}: max_id={max_id}")
    counters.update_one(
        {"_id": coll_name},
        {"$set": {"seq": max_id}},
        upsert=True
    )

print("All counters initialized successfully!")

