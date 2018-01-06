import mergelife

HEIGHT = 100
WIDTH = 100
TRACK_SIZE = 50
RULE = "E542-5F79-9341-F31E-6C6B-7F08-8773-7068"

def render(rule,height,width,steps,filename):
    ml_inst = mergelife.new_ml_instance(height, width, rule)
    mergelife.randomize_lattice(ml_inst)

    for i in range(steps):
        mergelife.update_step(ml_inst)

    mergelife.save_image(ml_inst,filename)


#ml_inst = mergelife.new_ml_instance(HEIGHT, WIDTH, RULE)
#score = mergelife.objective_function(ml_inst)
#print(score)

render("cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac",100,400,300,"c:\\jth\\fig1.eps")