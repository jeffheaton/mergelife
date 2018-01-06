from flask import Flask, abort, request
import json
import sys
import mergelife
import subprocess
import multiprocessing
import requests

HEIGHT = 100
WIDTH = 100
TRACK_SIZE = 50


if __name__ == '__main__':

    if len(sys.argv) != 3 and len(sys.argv) != 4 :
        print("eval_services start 5000 16")
        print("eval_services stop 5000 16")
        sys.exit(0)
    else:
        if len(sys.argv) == 4:
            sz = int(sys.argv[3])
        else:
            sz = multiprocessing.cpu_count()

        port = int(sys.argv[2])
        cmd = sys.argv[1].lower()

        if cmd == 'start':
            for i in range(sz):
                l = ["python", "eval_service.py", str(port+i)]
                print(l)
                subprocess.Popen(l)
        elif cmd == 'stop':
            for i in range(sz):
                try:
                    requests.get("http://localhost:{}/shutdown/".format(port + i))
                    print("{}: Stopped".format(port + i))
                except requests.exceptions.RequestException as e:
                    print("{}: Not running".format(port + i))