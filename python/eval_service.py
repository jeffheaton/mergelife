from flask import Flask, abort, request
import json
import sys
import mergelife

app = Flask(__name__)

def shutdown_server():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is None:
        raise RuntimeError('Not running with the Werkzeug Server')
    func()

@app.route('/eval/', methods=['POST'])
def eval():
    if not request.json:
        abort(500)

    rule_str = request.json['rule']
    width = int(request.json['width'])
    height = int(request.json['height'])
    cycles = int(request.json['cycles'])

    ml_inst = mergelife.new_ml_instance(height, width, rule_str)
    result = mergelife.objective_function(ml_inst,cycles)

    return json.dumps({'rule':rule_str,'score':str(result['score']),'time_step':str(result['time_step'])})

@app.route('/', methods=['GET'])
def root():
    return "merge-life:eval"

@app.route('/shutdown/', methods=['GET'])
def shutdown():
    shutdown_server()
    return 'Server shutting down...'

if __name__ == '__main__':
    if len(sys.argv) != 2:
        port = "5000"
    else:
        port = sys.argv[1]

    app.run(host="0.0.0.0", port=int(port),debug=True)