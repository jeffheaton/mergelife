import requests
r = requests.post("http://localhost:5000/eval/",
    headers={'content-type':'application/json'},
    json={'rule': '1e6b-9afa-16f2-23bc-3396-9fb3-46f4-cb4f','height': 100,'width':100,'cycles':5})
print(r.status_code, r.reason)
print(r.text)