import requests
import concurrent.futures
import threading
import mergelife
import sys
import json
from threading import Lock, Event
import time

services = []
request_lock = Lock()
request_event = Event()
HEIGHT = 100
WIDTH = 100

def request_service():
    while True:
        with request_lock:
            for svc in services:
                if not svc[0]:
                    svc[0] = True
                    return svc

        request_event.wait()


def process_request(args):
    steps = 0
    req = args[0]
    height = args[1]
    width = args[2]
    cycles = args[3]
    cb = args[4]

    score = -10 # Temp value
    if len(services) == 0:
        ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, req['rule'])
        score = mergelife.objective_function(ml_inst,cycles)
    else:
        try:
            svc = request_service()
            r = requests.post(svc[1]+"eval/",
                headers={'content-type': 'application/json'},
                json={'rule': req['rule'], 'height': str(height), 'width': str(width), 'cycles': str(cycles)})

            result = json.loads(r.text)
            score = result['score']
            steps = int(result['time_step'])


        except:
            print("Unexpected error:", sys.exc_info())

        finally:  # This is the correct syntax
            release_service(svc)

    req['score'] = score
    if cb is not None:
        cb(req)
    return steps

def ml_init_eval(hosts):

    for host in hosts:
        host_addr, host_port = host.split(':')
        host_port = int(host_port)

        i = 0
        done = False
        while not done:
            url = "http://{}:{}/".format(host_addr,host_port+i)

            try:
                requests.get(url)
                services.append([False, url])
                i+=1
            except requests.exceptions.RequestException as e:  # This is the correct syntax
                done = True

    return i

def release_service(svc):
    with request_lock:
        svc[0] = False
        request_event.set()

def evaluate_population(population,height,width, cycles, cb = None):
    global services

    threads_needed = len(services)

    if threads_needed == 0:
        threads_needed = 1

    executor = concurrent.futures.ThreadPoolExecutor(max_workers=threads_needed)

    list = []
    scored = 0

    start_time = time.time()
    for req in population:
        if req['score'] is None:
            scored+=1
            list.append(executor.submit(process_request, (req, height, width, cycles, cb)))


    list = [x.result() for x in list]
    steps = sum(list)
    executor.shutdown(wait=True)

    elapsed_time = time.time() - start_time
    time_per = elapsed_time / steps * 10000

    return { 'total_time': int(elapsed_time), 'time_step': steps, 'scored': scored, 'time_per_10k': time_per }


