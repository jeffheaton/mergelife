import matplotlib
matplotlib.use('TkAgg')

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.animation as animation
import mergelife
import sys

HEIGHT = 100
WIDTH = 100
TRACK_SIZE = 100
time_step = 0

fig = plt.figure()
data = np.random.randint(0,256, size=(HEIGHT, WIDTH, 3), dtype=np.uint8)
im = plt.imshow(data, animated=True)


def updatefig(*args):
    global ml_inst

    data2 = mergelife.update_step(ml_inst)
    if mergelife.is_lattice_stable(ml_inst):
        sys.exit()
    im.set_array(data2)

    return im,

def go_animate():
    ani = animation.FuncAnimation(fig, updatefig, interval=10, blit=True)
    plt.show()



# BLOBS - 87fd-4b49-5690-2b7f-5bbc-22ab-e326-0a58
# reptile - 46d-7581-778b-98e0-1212-0153-b6d9-de99
# snakes - de71-6c1a-d20f-2b97-607a-e251-2abc-7f21
# snakes2 -762f-faae-dc19-317f-27a4-5cf9-3018-974a
# cells - 1134-7bb1-9e4b-5b3c-5593-e772-0a9c-9066
# CELLS - 5296-e838-21ab-828a-92be-ca3b-b7a6-6cb4
# cells - e8de-c9ad-7b0e-54a2-a9fd-9412-2fe5-4c22
# Lifelike, spread - 7dc6-ae34-6f31-511a-1466-c1a7-4df8-51f3
# Lifelike, 5c7b-068d-ef6c-0eed-1307-17bd-d196-404e
# Lifelike, substrait, 474f-a2ec-5694-0f53-caa0-1579-ed70-8fab
# c31e-53c0-fd92-c7a2-4bb5-d93f-81b3-c0b6
# Clay, 74f9-b6fa-839c-72bb-49ed-82e3-28c0-2649
# 6761-56bc-d478-eca2-acdf-a109-ff83-6305
# Fire, 4a13-8c4b-78d3-755e-b933-b089-4fb0-53ee
# March, fe61-c6fb-5155-5abf-581a-3f4e-07d0-87e9
# Cells - 99ca-2383-f7fc-922c-d681-8abf-6207-6161
# Red World - E542-5F79-9341-F31E-6C6B-7F08-8773-7068
# Complex Substrait - 8343-7496-5c8a-612b-a0db-922a-95c0-324a
# cool maze - 9f1c-0d75-3cc1-4a1c-73d2-59ae-6cbf-2b8a
# cells - 1597-68c2-3624-4b7d-1f04-e82e-ba08-6cb9
# bars - ab32-29eb-2f17-814a-fe5c-b7d7-61c7-d712
# snakes - 79c7-9ec1-b3de-ba00-26bd-6f7e-7227-6207
# large single blob - ecf3-ed04-2f31-ee86-7373-446e-d062-7986
# small blobs - 8245-0bb9-e4fd-9d2d-6882-ca83-3293-9818
# Organic - 1c48-9004-8831-41be-2804-8f50-9901-db18
# blobs - 2985-d29b-9705-2309-bb8a-3f6b-626c-6a7f
# cool, d44f-9ba5-53e7-7373-fb07-9984-b444-727b

CODE_CONWAY = "a07f-c000-0000-0000-0000-0000-ff80-807f"
CODE_PLASMA = "362b-3f47-5db2-f7dd-3749-0484-d070-8d8e"
CODE_COOL = "cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac"
CODE_RED = "E542-5F79-9341-F31E-6C6B-7F08-8773-7068"
#CODE_TEST = mergelife.toHex(mergelife.random_update_rule())

#sorted_code = parse_code(CODE_CONWAY)
#sorted_code = parse_code(CODE_PLASMA)
#sorted_code = parse_code(CODE)

# CODE = "4af3-9216-c799-4501-4e58-50a6-ddca-ae26"

# DEAD
#CODE = "03c3-62b4-458a-0d6e-6d2d-1d1d-7afc-ec9b"
#CODE = "3e69-82bb-ee4a-f54e-841e-acb7-6664-f541"
#CODE = "4af3-9216-c799-4501-4e58-50a6-ddca-ae26"
#CODE = "6b46-687f-4885-4988-0a56-efc7-c494-9bfb"
#CODE = "6bfb-5015-58ab-8161-8d70-c743-a8ba-34d3"
#CODE = "7cd2-a302-364a-47ad-fe92-1eab-0d8e-d47b"
#CODE = "9cd5-588b-9532-4af0-7816-af1f-23a1-6683"
#CODE = "31d0-16fd-626d-68ed-9dfe-bea9-1997-7d51"
#CODE = "745b-d779-6d22-a650-32d7-c272-9cab-6e66"
#CODE = "31d0-16fd-626d-68ed-9dfe-bea9-1997-7d51"
#CODE = "79e4-d466-6d3f-1cb6-83ff-df8c-c73c-4154"
#CODE = "9cd5-588b-9532-4af0-7816-af1f-23a1-6683"
#CODE = "3392-112d-6f45-73bd-075b-5223-25a6-e28b"
#CODE = "620c-0efc-3d88-7cf2-14d8-d960-58ab-9b78"
#CODE = "56ac-691f-d2ba-46cd-80db-379b-a7aa-0c17"

# Good
CODE = "a07f-c000-0000-0000-0000-0000-ff80-807f"
#CODE = "eaa3-fb3c-4d19-e732-7376-9601-764b-e810"
#CODE = "a07f-c000-0000-0000-0000-0000-ff80-807f"
#CODE = "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c"
#CODE = "E542-5F79-9341-F31E-6C6B-7F08-8773-7068"

#CODE = "fe57-8463-7b36-706f-ba70-0980-bb8f-3aa9"


print(CODE)
ml_inst = mergelife.new_ml_instance(HEIGHT,WIDTH,CODE)
ml_inst['track_size'] = TRACK_SIZE

go_animate()
