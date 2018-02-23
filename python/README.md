# Instructions for Running MergeLife Python

## Viewing a CA

It is easiest just to use the [Javascript viewer](http://www.heatonresearch.com/mergelife/).  However, I do provide a Python viewer too.  

The Python viewer will launch a Matplotlib window and animate the CA.  The value
CODE in the file ml_viewer.py specifies the ML rule that is to be viewed.
There are many CODE values to choose from in the comments of the source code.

```
python ml_viewer.py
```

## Evloving a CA

I currently do not provide a Javascript evolver.  A JS evolver is under
development.  For now, Python is the best way to evolve a ML rule.  This
is processor intense, and as a result, I use a web service architecture.
This solves the problem of the Python GIL (global instruction lock).  
First, you must start up the scoring service.  This is done with the following
command:

```
python eval_services.py start 5000
```

This will startup a number of services that is equal to the number of cores
on your machine.  This starts at port 5000.  It would not be difficult to
extend the code to support multiple machines.  I have not yet added this
capability.

Once the scoring services are started, you can begin evolving.  From a
different terminal window, execute the following:

```
python ml_evolve.py
```

This program will run endlessly and produce output similar to the following:

```
Found: 8 handler(s).
Generating and evaluating initial population.
Beginning epochs.
1:{'score': '2.4650153711023277', 'rule': '86e2-d978-34eb-1825-24f0-b08a-f54c-5b16'}:epoch time:0:00:33.00,time 10K steps:0:00:13.07
2:{'score': '2.4650153711023277', 'rule': '86e2-d978-34eb-1825-24f0-b08a-f54c-5b16'}:epoch time:0:00:13.00,time 10K steps:0:00:07.34
3:{'score': '2.4650153711023277', 'rule': '86e2-d978-34eb-1825-24f0-b08a-f54c-5b16'}:epoch time:0:00:15.00,time 10K steps:0:00:07.64
4:{'score': '2.4650153711023277', 'rule': '86e2-d978-34eb-1825-24f0-b08a-f54c-5b16'}:epoch time:0:01:20.00,time 10K steps:0:00:13.47
5:{'score': '2.4650153711023277', 'rule': '86e2-d978-34eb-1825-24f0-b08a-f54c-5b16'}:epoch time:0:00:49.00,time 10K steps:0:00:13.77
6:{'score': '2.4650153711023277', 'rule': '86e2-d978-34eb-1825-24f0-b08a-f54c-5b16'}:epoch time:0:00:27.00,time 10K steps:0:00:12.39
7:{'rule': '86e2-df44-34eb-1825-24f0-b08a-f54c-5b16', 'score': '2.493842775581906'}:epoch time:0:00:15.00,time 10K steps:0:00:08.02
8:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:30.00,time 10K steps:0:00:11.09
9:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:43.00,time 10K steps:0:00:14.58
10:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:46.00,time 10K steps:0:00:11.40
11:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:56.00,time 10K steps:0:00:12.78
12:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:48.00,time 10K steps:0:00:09.70
13:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:59.00,time 10K steps:0:00:11.76
14:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:48.00,time 10K steps:0:00:10.36
15:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:48.00,time 10K steps:0:00:11.19
16:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:46.00,time 10K steps:0:00:11.24
17:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:44.00,time 10K steps:0:00:10.15
18:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:57.00,time 10K steps:0:00:09.76
19:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:00:46.00,time 10K steps:0:00:08.89
20:{'rule': '86e2-df44-34eb-131b-f4f0-b08a-f54c-5b16', 'score': '2.8834079929732104'}:epoch time:0:01:03.00,time 10K steps:0:00:10.54
21:{'rule': 'a1ad-79cb-47ed-3a78-f81f-4741-941c-2278', 'score': '2.9667758328627896'}:epoch time:0:00:38.00,time 10K steps:0:00:09.24
22:{'rule': 'a1ad-79cb-47ed-3a78-f81f-4741-941c-2278', 'score': '2.9667758328627896'}:epoch time:0:01:16.00,time 10K steps:0:00:11.28
23:{'rule': 'a2c7-79cb-47ed-3a78-24f0-4741-941c-2278', 'score': '3.103381642512077'}:epoch time:0:01:04.00,time 10K steps:0:00:10.07
24:{'rule': 'a2c7-79cb-47ed-3a78-24f0-4741-941c-2278', 'score': '3.103381642512077'}:epoch time:0:00:37.00,time 10K steps:0:00:09.13
```

You can run as long as you like.  The rule and score are displayed.  You might want to copy/paste
the rule into the [Javascript viewer](http://www.heatonresearch.com/mergelife/) to check progress.  The score will eventually converge to a local minima.  
