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

# 1. Clean test branches (keep _id <= 5) and restore original names & states
branches_to_delete = [b['_id'] for b in db.branches.find() if b['_id'] > 5]
if branches_to_delete:
    r = db.branches.delete_many({'_id': {'$in': branches_to_delete}})
    print(f"Deleted {r.deleted_count} test branches: {branches_to_delete}")

canonical_branches = [
    (1, "Manila Main Branch", "123 Escolta St., Binondo, Manila", "0917-111-2222", "09:00:00", "18:00:00"),
    (2, "Makati Poblacion Branch", "456 General Luna St., Poblacion, Makati", "0917-333-4444", "09:00:00", "18:00:00"),
    (3, "BGC High Street Branch", "Unit 204, Bonifacio High Street, Taguig", "0917-555-6666", "09:00:00", "18:00:00"),
    (4, "QC Tomas Morato Branch", "78 Tomas Morato Ave., Quezon City", "0917-777-8888", "09:00:00", "18:00:00"),
    (5, "Ortigas Center Branch", "Emerald Ave. cor. Doña Julia Vargas, Pasig", "0917-999-0000", "09:00:00", "18:00:00"),
]
for bid, bname, baddr, bphone, bopen, bclose in canonical_branches:
    db.branches.update_one(
        {'_id': bid},
        {'$set': {
            'name': bname,
            'address': baddr,
            'phone_number': bphone,
            'opening_time': bopen,
            'closing_time': bclose,
            'is_active': True
        }},
        upsert=True
    )

# 2. Clean test barbers (keep _id <= 6) and restore original names & states
barbers_to_delete = [b['_id'] for b in db.barbers.find() if b['_id'] > 6]
if barbers_to_delete:
    r = db.barbers.delete_many({'_id': {'$in': barbers_to_delete}})
    print(f"Deleted {r.deleted_count} test barbers: {barbers_to_delete}")

canonical_barbers = [
    (1, 2, "Ramon", "Master barber specializing in classic cuts and beard styling.", "Classic Cuts, Beard Sculpting", 1),
    (2, 3, "Dennis", "Precision stylist with 10+ years experience in modern fades.", "Modern Fades, Shaves", 1),
    (3, 4, "Miguel", "Creative colorist and artistic stylist.", "Hair Color, Texture Styling", 2),
    (4, 5, "Carlos", "Master fade specialist and beard sculpting artist.", "Taper Fades, Pompadours, Beard Grooming", 3),
    (5, 6, "Rafael", "Traditional wet shave artisan and hot towel specialist.", "Hot Towel Shave, Scissor Cuts", 4),
    (6, 7, "Anton", "Executive stylist known for clean contours and pompadours.", "Executive Cuts, Texture Styling", 5),
]
for bid, uid, dname, bio, specs, branch_id in canonical_barbers:
    db.barbers.update_one(
        {'_id': bid},
        {'$set': {
            'user_id': uid,
            'display_name': dname,
            'bio': bio,
            'specialties': specs,
            'branch_id': branch_id,
            'is_active': True
        }},
        upsert=True
    )

# Ensure users 1..19 are active
db.users.update_many({'_id': {'$lte': 19}}, {'$set': {'is_active': True}})
db.users.update_one({'_id': 2}, {'$set': {'full_name': 'Ramon'}})
db.users.update_one({'_id': 6}, {'$set': {'full_name': 'Rafael'}})

# 3. Clean test services (keep _id <= 4)
services_to_delete = [s['_id'] for s in db.services.find() if s['_id'] > 4]
if services_to_delete:
    r = db.services.delete_many({'_id': {'$in': services_to_delete}})
    print(f"Deleted {r.deleted_count} test services: {services_to_delete}")

canonical_services = [
    (1, "Signature Cut", "Complete precision haircut, hot towel finish, style", 350.0, 30),
    (2, "Beard Sculpt & Trim", "Precision edging, hot lather, beard oil conditioning", 250.0, 25),
    (3, "Hair & Beard Deluxe", "Signature cut combined with full beard sculpture", 550.0, 50),
    (4, "Royal Shave Treatment", "Multi-step traditional hot towel razor shave with facial massage", 400.0, 40)
]
for sid, sname, sdesc, sprice, sdur in canonical_services:
    db.services.update_one(
        {'_id': sid},
        {'$set': {
            'name': sname,
            'description': sdesc,
            'price': sprice,
            'duration_minutes': sdur,
            'is_active': True
        }},
        upsert=True
    )

# 4. Clean test products (keep _id <= 5)
products_to_delete = [p['_id'] for p in db.products.find() if p['_id'] > 5]
if products_to_delete:
    r = db.products.delete_many({'_id': {'$in': products_to_delete}})
    print(f"Deleted {r.deleted_count} test products: {products_to_delete}")
    r_sm = db.stock_movements.delete_many({'product_id': {'$in': products_to_delete}})
    print(f"Deleted {r_sm.deleted_count} test stock movements")

# 5. Clean test equipment (keep _id <= 5)
equipment_to_delete = [e['_id'] for e in db.equipment.find() if e['_id'] > 5]
if equipment_to_delete:
    r = db.equipment.delete_many({'_id': {'$in': equipment_to_delete}})
    print(f"Deleted {r.deleted_count} test equipment: {equipment_to_delete}")
    r_ea = db.equipment_assignments.delete_many({'equipment_id': {'$in': equipment_to_delete}})
    r_ec = db.equipment_checks.delete_many({'equipment_id': {'$in': equipment_to_delete}})
    print(f"Deleted {r_ea.deleted_count} test equipment assignments and {r_ec.deleted_count} checks")

# 6. Clean test users (keep _id <= 19 and _id == 40 'Josh')
users_to_delete = [u['_id'] for u in db.users.find() if u['_id'] > 19 and u['_id'] != 40]
if users_to_delete:
    r = db.users.delete_many({'_id': {'$in': users_to_delete}})
    print(f"Deleted {r.deleted_count} test users: {users_to_delete}")
    db.user_roles.delete_many({'user_id': {'$in': users_to_delete}})

# 7. Reset sequential counters
def reset_counter(coll_name, id_field='_id'):
    max_doc = db[coll_name].find_one(sort=[(id_field, -1)])
    max_id = max_doc[id_field] if max_doc and id_field in max_doc else 0
    db.counters.update_one({'_id': coll_name}, {'$set': {'seq': max_id}}, upsert=True)
    print(f"Counter for {coll_name} set to {max_id}")

reset_counter('branches')
reset_counter('users')
reset_counter('barbers')
reset_counter('services')
reset_counter('products')
reset_counter('equipment')
reset_counter('appointments')

print("All placeholder and test items have been cleaned from the database!")
