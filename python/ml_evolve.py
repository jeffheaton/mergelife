import mergelife
import eval_client
import numpy as np
import operator

POPULATION_SIZE = 100
HEIGHT = 100
WIDTH = 100
CYCLES = 5

def hms_string(sec_elapsed):
    h = int(sec_elapsed / (60 * 60))
    m = int((sec_elapsed % (60 * 60)) / 60)
    s = sec_elapsed % 60
    return "{}:{:>02}:{:>05.2f}".format(h, m, s)

def mutate(genome):
    h = "0123456789abcdef"
    done = False
    while not done:
        i = np.random.randint(0,len(genome))
        if genome[i] != '-':
            i2 = np.random.randint(0,len(h))
            result = genome[:i] + h[i2] + genome[(i+1):]
            if result != genome:
                done = True
    return result

def crossover(genome1,genome2,cut_length):
    # The genome must be cut at two positions, determine them
    cutpoint1 = np.random.randint(len(genome1) - cut_length)
    cutpoint2 = cutpoint1 + cut_length

	# Produce two offspring
    c1 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]
    c2 = genome1[0:cutpoint1] + genome2[cutpoint1:cutpoint2] + genome1[cutpoint2:]

    return [c1,c2]


threads_needed = eval_client.ml_init_eval(["localhost:5000"])

if threads_needed==0:
    print("Did not find any handlers, will run local.")
    threads_needed = 1
else:
    print("Found: {} handler(s).".format(threads_needed))

print("Generating and evaluating initial population.")
population = [ {'score': None, 'rule':mergelife.random_update_rule()} for i in range(POPULATION_SIZE)]
#population[0]['rule'] = "E542-5F79-9341-F31E-6C6B-7F08-8773-7068"
#population[0]['rule'] = "a07f-c000-0000-0000-0000-0000-ff80-807f"

stats = eval_client.evaluate_population(population,HEIGHT,WIDTH,CYCLES)
population = sorted(population, key=lambda k: k['score'], reverse=True)
print("Beginning epochs.")

def select_tournament(population,rounds,cmp=operator.gt):
    result = np.random.randint(0,len(population))

    for i in range(rounds):
        challenger = np.random.randint(0,len(population))
        if cmp(population[challenger]['score'], population[result]['score']):
            result = challenger
            break

    return result


def process_epoch(population):
    children = []

    while( len(children)< 20 ):
        if np.random.uniform()<0.2:
            # Mutate
            parent_idx = select_tournament(population,5,operator.gt)
            parent = population[parent_idx]['rule']
            child = mutate(parent)
            children.append({'rule':child,'score':None})
        else:
            # Crossover
            parent1_idx = select_tournament(population,5,operator.gt)
            parent2_idx = parent1_idx
            while parent1_idx == parent2_idx:
                parent2_idx = select_tournament(population,5,operator.gt)

            parent1 = population[parent1_idx]['rule']
            parent2 = population[parent2_idx]['rule']

            if parent1 != parent2:
                child1, child2 = crossover(parent1,parent2,5)
                children.append({'rule':child1,'score':None})
                children.append({'rule':child2,'score':None})

    eval_stats = eval_client.evaluate_population(children,HEIGHT,WIDTH,CYCLES)

    for child in children:
        target_idx = select_tournament(population,5,operator.lt)
        population[target_idx] = child

    return eval_stats


for i in range(10000):
    eval_stats = process_epoch(population)
    population = sorted(population, key=lambda k: k['score'], reverse=True)
    print("{}:{}:epoch time:{},time 10K steps:{}".format(i+1,population[0],
          hms_string(eval_stats['total_time']),hms_string(eval_stats['time_per_10k'])))
