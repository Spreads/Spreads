# Makefile for the MinGW suite for Windows
CC = gcc  # clang++, g++ or just gcc if not compiling Snappy (C++ code)
#AR	= ar
#W	= -W -Wall -Wno-unused-parameter -Wbad-function-cast -Wuninitialized
THREADS = -pthread
# (A) Production use (optimized mode)
OPT ?= -O3 #-DNDEBUG
# (B) Debug mode, w/ full line-level debugging symbols
#OPT ?= -O2 # -g
# (C) Profiling mode: opt, but w/debugging symbols
# OPT ?= -O2 -g
XCFLAGS = -mavx2 #
#-DSQLITE_ENABLE_RTREE -DSQLITE_ENABLE_JSON1 -DSQLITE_ENABLE_RBU

CFLAGS =   $(THREADS) $(OPT) $(W) $(XCFLAGS)
LDFLAGS = -lpthread  # for UNIX or for Windows with pthread installed
LDFLAGS =  -static -static-libgcc #-static-libstdc++ # for mingw

SOURCES = $(wildcard ./blosc/*.c) #./blosc/*.c
#HEADERS = 
CFLAGS += -Iblosc

# Support for internal LZ4
LZ4_DIR = ./internal-complibs/lz4-1.7.5
CFLAGS += -DHAVE_LZ4 -I$(LZ4_DIR)
SOURCES += $(wildcard $(LZ4_DIR)/*.c)

ZSTD_DIR = ./internal-complibs/zstd-1.1.2
CFLAGS += -DHAVE_LZ4 -I$(ZSTD_DIR)
SOURCES += $(wildcard $(ZSTD_DIR)/*.c)

DESTDIR = out/
CPUx ?= 32
prefix	= w$(CPUx)

NAME = libblosc
LIBRARY = $(NAME).a
SHARED = $(NAME).dll #$(CPUx)

LIBOBJECTS = $(SOURCES:.c=.o)

all: $(LIBRARY) $(SHARED)

clean:
	rm -rf *.exe *.dll *.lib *.def *.[ao] *.[ls]o  $(LIBOBJECTS)

$(LIBRARY): $(LIBOBJECTS) $(HEADERS)
	rm -f $@
	$(AR) -rs $@ $(LIBOBJECTS)
	
%:	%.o
	$(CC) $(CFLAGS) $(LDFLAGS) $^ $(LDLIBS) -o $@
	
.c.o:
	$(CC) $(CFLAGS) -c $< -o $@
	
dll : $(SHARED)
	
$(SHARED):	$(SOURCES)
	$(CC)  $(CFLAGS) $(LDFLAGS) -shared -Wl,--output-def=$(NAME).def -Wl,--out-implib=$(NAME).lib -Wl,-soname -Wl,--dll $(SOURCES) -o $@ $(LDLIBS) $(THREADS) -s 
	
	
install: all
	rm -rf $(DESTDIR)$(prefix)/; mkdir -p $(DESTDIR)$(prefix)/;
	
	rm -rf $(DESTDIR)$(prefix)/bin/; mkdir $(DESTDIR)$(prefix)/bin/;
	for f in $(NAME).dll $(NAME).def $(NAME).lib; do cp $$f $(DESTDIR)$(prefix)/bin; done
	
	rm -rf $(DESTDIR)$(prefix)/lib/; mkdir $(DESTDIR)$(prefix)/lib/;
	cp $(NAME).a $(DESTDIR)$(prefix)/lib; 
	
	rm -rf $(DESTDIR)$(prefix)/include/; mkdir $(DESTDIR)$(prefix)/include/;
	for f in $(HEADERS); do cp $$f $(DESTDIR)$(prefix)/include; done
	