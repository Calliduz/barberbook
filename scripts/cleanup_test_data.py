import os
import pymongo

uri = os.environ.get('MONGODB_URI')
if not uri:
    with open('.env') as f:
        for line in f:
            if line.startswith('MONGODB_URI='):
                uri = line.strip().split('=', 1)[1]
                break

client = pymongo.MongoClient(uri)
db = client['barberbook']

print("Connected to MongoDB:", db.name)

# 1. Clean test barbers (keep _id <= 6)
barbers_to_delete = [b['_id'] for b in db.barbers.find() if b['_id'] > 6]
if barbers_to_delete:
    r = db.barbers.delete_many({'_id': {'$in': barbers_to_delete}})
    print(f"Deleted {r.deleted_count} test barbers: {barbers_to_delete}")

# 2. Clean test products (keep _id <= 5)
products_to_delete = [p['_id'] for p in db.products.find() if p['_id'] > 5]
if products_to_delete:
    r = db.products.delete_many({'_id': {'$in': products_to_delete}})
    print(f"Deleted {r.deleted_count} test products: {products_to_delete}")
    # Also clean associated stock movements and sales
    r_sm = db.stock_movements.delete_many({'product_id': {'$in': products_to_delete}})
    print(f"Deleted {r_sm.deleted_count} test stock movements")

# 3. Clean test equipment (keep _id <= 5)
equipment_to_delete = [e['_id'] for e in db.equipment.find() if e['_id'] > 5]
if equipment_to_delete:
    r = db.equipment.delete_many({'_id': {'$in': equipment_to_delete}})
    print(f"Deleted {r.deleted_count} test equipment: {equipment_to_delete}")
    r_ea = db.equipment_assignments.delete_many({'equipment_id': {'$in': equipment_to_delete}})
    r_ec = db.equipment_checks.delete_many({'equipment_id': {'$in': equipment_to_delete}})
    print(f"Deleted {r_ea.deleted_count} test equipment assignments and {r_ec.deleted_count} checks")

# 4. Clean test users (keep _id <= 19 and _id == 40 'Josh')
users_to_delete = [u['_id'] for u in db.users.find() if u['_id'] > 19 and u['_id'] != 40]
if users_to_delete:
    r = db.users.delete_many({'_id': {'$in': users_to_delete}})
    print(f"Deleted {r.deleted_count} test users: {users_to_delete}")
    db.user_roles.delete_many({'user_id': {'$in': users_to_delete}})

# 5. Clean test appointments (keep reference_code starting with 'BB-')
appts_to_delete = [a['_id'] for a in db.appointments.find() if not a.get('reference_code', '').startswith('BB-')]
if appts_to_delete:
    r = db.appointments.delete_many({'_id': {'$in': appts_to_delete}})
    print(f"Deleted {r.deleted_count} test appointments: {appts_to_delete}")
    db.status_history.delete_many({'appointment_id': {'$in': appts_to_delete}})

# 6. Reset sequential counters
def reset_counter(coll_name, id_field='_id'):
    max_doc = db[coll_name].find_one(sort=[(id_field, -1)])
    max_id = max_doc[id_field] if max_doc and id_field in max_doc else 0
    db.counters.update_one({'_id': coll_name}, {'$set': {'seq': max_id}}, upsert=True)
    print(f"Counter for {coll_name} set to {max_id}")

reset_counter('users')
reset_counter('barbers')
reset_counter('products')
reset_counter('equipment')
reset_counter('appointments')

print("All placeholder and test items have been cleaned from the database!")
