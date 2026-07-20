CC      ?= cc
CFLAGS  ?= -std=c11 -O3 -Wall -Wextra -Wno-unused-parameter
LDFLAGS ?=
LDLIBS  ?= -lm -lz -lpthread

# -march=native gives the vectorizer the widest registers available.  Drop it
# if you need the binary to run on a different machine than it was built on.
NATIVE  ?= 1
ifeq ($(NATIVE),1)
CFLAGS  += -march=native
endif

SRC := $(wildcard src/*.c)
OBJ := $(SRC:.c=.o)
BIN := mergelife-trainer
LIBOBJ := $(filter-out src/main.o,$(OBJ))

all: $(BIN)

$(BIN): $(OBJ)
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ $(OBJ) $(LDLIBS)

src/%.o: src/%.c src/mergelife.h
	$(CC) $(CFLAGS) -c -o $@ $<

# Fuzzes the optimized CA engine against a naive transcription of the
# reference Java implementation.
verify: test/verify.c $(LIBOBJ)
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ test/verify.c $(LIBOBJ) $(LDLIBS)

test: verify
	./verify

clean:
	rm -f $(OBJ) $(BIN) verify

.PHONY: all clean test
