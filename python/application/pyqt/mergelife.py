import ctypes
import scipy
import numpy as np
from scipy.ndimage import convolve
import logging
import dp
from PIL import Image

logger = logging.getLogger(__name__)

# The color table.
COLOR_TABLE = [
    [0, 0, 0],  # Black 0
    [255, 0, 0],  # Red 1
    [0, 255, 0],  # Green 2
    [255, 255, 0],  # Yellow 3
    [0, 0, 255],  # Blue 4
    [255, 0, 255],  # Purple 5
    [0, 255, 255],  # Cyan 6
    [255, 255, 255]  # White 7
]

def toHex(code):
    result = ""

    for rng, pct in code:
        if len(result) > 0:
            result += "-"
        result += "%02x" % int(rng)
        result += "%02x" % ctypes.c_ubyte(int(pct)).value

    return result


def fromHex(str):
    result = []
    str = str.replace('-', '')
    for i in range(len(COLOR_TABLE)):
        idx = i * 4
        rng = str[idx:idx + 2]
        pct = str[idx + 2:idx + 4]
        rng = int(rng, 16)
        pct = int(pct, 16)

        pct = ctypes.c_byte(pct).value  # Twos complement
        result.append((rng, pct))

    return result

def parse_update_rule(code):
    code = fromHex(code)

    sorted_code = []
    for i, x in enumerate(code):
        rng = int(x[0] * 8)
        if x[1] > 0:
            pct = x[1] / 127.0
        else:
            pct = x[1] / 128.0
        sorted_code.append((2048 if rng == 2040 else rng, pct, i))

    sorted_code = sorted(sorted_code)
    return sorted_code

def randomize_lattice(ml_instance):
    height = ml_instance['height']
    width = ml_instance['width']
    ml_instance['track'] = {}
    ml_instance['time_step'] = 0
    ml_instance['lattice'][0]['data'] = np.random.randint(0, 256, size=(height, width, 3), dtype=np.uint8)
    ml_instance['lattice'][1]['data'] = np.copy(ml_instance['lattice'][0]['data'])


def new_ml_instance(height, width, rule_str):
    result = {
        'height': height,
        'width': width,
        'rule_str': rule_str,
        'sorted_rule': parse_update_rule(rule_str),
        'time_step': 0,
        'track': {},
        'lattice': [
            {'data': None, 'eval': None},
            {'data': None, 'eval': None}
        ]
    }

    randomize_lattice(result)
    result['lattice'][1]['data'] = np.copy(result['lattice'][0]['data'])
    return result

def update_step(ml_instance):
    kernel = [[1, 1, 1], [1, 0, 1], [1, 1, 1]]
    THIRD = 1.0 / 3.0

    # Get important values
    sorted_rule = ml_instance['sorted_rule']
    height = ml_instance['height']
    width = ml_instance['width']
    changed = np.zeros((height, width), dtype=bool)

    # Swap lattice
    t = ml_instance['lattice'][1]
    ml_instance['lattice'][1] = ml_instance['lattice'][0]
    ml_instance['lattice'][0] = t

    # Get current and previous lattice
    prev_data = ml_instance['lattice'][1]['data']
    current_data = ml_instance['lattice'][0]['data']

    # Merge RGB
    data_avg = np.dot(prev_data, [THIRD, THIRD, THIRD])
    data_avg = data_avg.astype(int)
    pad_val = scipy.stats.mode(data_avg, axis=None)[0]
    pad_val = int(pad_val)
    data_cnt = convolve(data_avg, kernel, cval=pad_val, mode='constant')

    # Perform update
    for limit, pct, cidx in sorted_rule:
        mask = data_cnt < limit
        mask = np.logical_and(mask, np.logical_not(changed))
        changed = np.logical_or(changed, mask)

        if pct < 0:
            pct = abs(pct)
            cidx = cidx + 1
            if cidx >= len(COLOR_TABLE):
                cidx = 0

        d = COLOR_TABLE[cidx] - prev_data[mask]
        current_data[mask] = prev_data[mask] + np.floor(d * pct)
        ml_instance['lattice'][0]['eval'] = {
            'mode': pad_val,
            'merge': data_avg,
            'neighbor': data_cnt
        }

    ml_instance['time_step'] += 1
    return current_data

def random_update_rule():
    result = []
    for i in range(len(COLOR_TABLE)):
        rng = np.random.randint(0, 255)
        pct = np.random.randint(-128, 128)

        result.append((rng, pct))
    return toHex(result)

def calc_objective_stats(ml_instance):
    height = ml_instance['height']
    width = ml_instance['width']
    time_step = ml_instance['time_step']

    e1 = ml_instance['lattice'][0]['eval']
    e2 = ml_instance['lattice'][1]['eval']

    if e1 is None or e2 is None:
        return {'mage': 0, 'mode': 0, 'mc': 0, 'bg': 0, 'fg': 0, 'act': 0, 'chaos': 0}

    d1_avg = e1['merge']
    d2_avg = e2['merge']

    # What percent of the grid is the mode, what percent is the background
    md1 = e1['mode']
    md2 = e2['mode']

    mode_mask = (d2_avg == md2)
    # mode_equal = sum(mode_equal.ravel()) / (height*width)

    # has been mode for >5
    if 'eval-md-cnt' in ml_instance['track']:
        mode_cnt = ml_instance['track']['eval-md-cnt']
    else:
        mode_cnt = np.zeros((height, width), dtype=int)
        ml_instance['track']['eval-md-cnt'] = mode_cnt

    mode_cnt[np.logical_not(mode_mask)] = 0
    mode_cnt += mode_mask

    mc = np.sum(mode_cnt > 50)

    # has been color (not mode) for >5
    if 'eval-same-cnt' in ml_instance['track']:
        same_cnt = ml_instance['track']['eval-same-cnt']
    else:
        same_cnt = np.zeros((height, width), dtype=int)
        ml_instance['track']['eval-same-cnt'] = same_cnt

    same_mask = (d1_avg == d2_avg)
    same_mask = np.logical_and(same_mask, np.logical_not(mode_mask))
    same_cnt[np.logical_not(same_mask)] = 0
    same_cnt += same_mask

    sc = np.sum(same_cnt > 5)

    # How long has the mode been its value
    mage = ml_instance['track'].get("eval-mode-age", 0)
    lm = ml_instance['track'].get('eval-last-mode-val', md2)
    if lm != md2:
        lm = md2
        mage = 0
    else:
        mage += 1

    # how long ago was a pixel the mode
    if 'eval-last-mode' in ml_instance['track']:
        last_mode = ml_instance['track']['eval-last-mode']
    else:
        last_mode = np.zeros((height, width), dtype=int)
        ml_instance['track']['eval-last-mode'] = last_mode

    last_mode[mode_mask] = time_step
    ml_instance['track']['eval-mode-age'] = mage
    ml_instance['track']['eval-last-mode-val'] = lm

    # Find the active cells
    # An active cell has not been a background cell for 5 steps, but was a background cell in the last 25 steps
    if time_step >= 25:
        t = (ml_instance['time_step'] - last_mode)
        t1 = t > 5
        t2 = t < 25
        t = np.logical_and(t1, t2)
        sp = np.sum(t)
    else:
        sp = 0

    # Combine
    size = height * width
    cnt_bg = mc
    cnt_fg = sc
    cnt_act = sp
    cnt_chaos = (height * width) - (cnt_bg + cnt_fg + cnt_act)

    cnt_bg /= size
    cnt_fg /= size
    cnt_act /= size
    cnt_chaos /= size

    logger.debug("{}:Mode Count: {}, Stable BG: {}, Stable FG: {}, Active: {}, Chaos: {}, Mage: {}".format(
        ml_instance['time_step'], mc, cnt_bg,
        cnt_fg, cnt_act, cnt_chaos, mage))

    return {'mage': mage, 'mode': md2, 'mc': mc, 'bg': cnt_bg, 'fg': cnt_fg, 'act': cnt_act, 'chaos': cnt_chaos}


def is_lattice_stable(ml_instance, o=None):
    if o is None:
        o = calc_objective_stats(ml_instance)

    cnt_bg = o['bg']

    if ml_instance['time_step'] > 100:
        if cnt_bg < 0.01:
            return True

    # Time to stop?
    mc_nochange = ml_instance['track'].get('eval-mc-nochange', 0)
    last_mc = ml_instance['track'].get('eval-last-mc', 0)

    if last_mc == o['mc']:
        mc_nochange += 1
        if mc_nochange > 100:
            return True
    else:
        mc_nochange = 0
        last_mc = o['mc']

    ml_instance['track']['eval-mc-nochange'] = mc_nochange
    ml_instance['track']['eval-last-mc'] = last_mc

    return ml_instance['time_step'] > 1000


def count_discrete(ml_instance):
    states = set()
    lattice = ml_instance['lattice'][0]['data']
    for row in range(lattice.shape[0]):
        for col in range(lattice.shape[1]):
            states.add(str(lattice[row][col]))
    return len(states)

def calc_stat_largest_rect(ml_inst,o):
    height = ml_inst['height']
    width = ml_inst['width']
    e2 = ml_inst['lattice'][1]['eval']
    mr = dp.max_size(e2['merge'], o['mode'])
    return (mr[0] * mr[1]) / (height * width)


def calc_objective_function(ml_inst, objective, dump=False):

    randomize_lattice(ml_inst)

    done = False
    while not done:
        update_step(ml_inst)
        o = calc_objective_stats(ml_inst)
        if dump:
            print(o)
        done = is_lattice_stable(ml_inst, o)

    stats = {
        'steps': ml_inst['time_step'],
        'foreground': o['fg'],
        'active': o['act'],
        'rect': calc_stat_largest_rect(ml_inst,o),
        'mage' : o['mage']
    }

    score = 0
    for rule in objective:
        actual = stats[rule['stat']]
        rule_min = rule['min']
        rule_max = rule['max']
        range = rule_max - rule_min
        ideal = range / 2

        if actual < rule_min:
            # too small
            adjust = rule['min_weight']
        elif actual > rule_max:
            # too big
            adjust = rule['max_weight']
        else:
            adjust = ((range / 2) - abs(actual - ideal)) / (range / 2)
            adjust *= rule['weight']
            pass

        score += adjust
        #print("{}:{}:{}".format(rule,actual,adjust))
    #print(score)

    return {'time_step':ml_inst['time_step'],'score':score}


def objective_function(ml_inst,cycles,objective,dump=False):
    lst = []
    steps = 0
    for i in range(cycles):
        if dump:
            print("Cycle #{}".format(i))
        result = calc_objective_function(ml_inst,objective,dump)
        lst.append(result['score'])
        steps+=result['time_step']
    return {'time_step':steps,'score':np.max(lst)}

def save_image(ml_instance, filename):
    lattice = ml_instance['lattice'][0]['data']
    newimage = Image.new('RGB', (len(lattice[0]), len(lattice)))  # type, size
    newimage.putdata([tuple(p) for row in lattice for p in row])
    newimage.save(filename)  # takes type from filename extension