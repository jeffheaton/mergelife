import matplotlib
import numpy as np
import mergelife
import sys

CODE = "a07f-c000-0000-0000-0000-0000-ff80-807f"



#CODE = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068"
#CODE = "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c"
#CODE = "8503-5eb6-084c-04df-7657-a5b3-6044-3524"
#CODE = "6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c"
#CODE = "df1d-bba1-8e06-aa66-48ff-7414-6a2f-6237"


HEIGHT = 100
WIDTH = 400
STEPS = 1000

ml_inst = mergelife.new_ml_instance(HEIGHT,WIDTH,CODE)

for i in range(STEPS):
    data = mergelife.update_step(ml_inst)

mergelife.save_image(ml_inst,"c:\\jth\\" + CODE + ".tiff")
