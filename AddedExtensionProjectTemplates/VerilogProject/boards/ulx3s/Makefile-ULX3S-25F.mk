.PHONY: all
.DELETE_ON_ERROR:
TOPMOD  := top
VLOGFIL := boards/ulx3s/$(TOPMOD).v
VCDFILE := $(TOPMOD).vcd
SIMPROG := $(TOPMOD)_tb
RPTFILE := $(TOPMOD).rpt
BINFILE := $(TOPMOD).bin
SIMFILE := $(SIMPROG).cpp
VDIRFB  := ./obj_dir
BUILD_DIR ?= build
ULX3S_BUILD_DIR := $(BUILD_DIR)/ulx3s-25k
ULX3S_JSON := $(ULX3S_BUILD_DIR)/top.json
ULX3S_CONFIG := $(ULX3S_BUILD_DIR)/ulx3s_out.config
ULX3S_BIT := $(ULX3S_BUILD_DIR)/ulx3s.bit
ULX3S_LOG := $(ULX3S_BUILD_DIR)/ulx3s.yslog
ULX3S_LPF := boards/ulx3s/ulx3s_v20.lpf
#COSIMS  := uartsim.cpp
all: $(ULX3S_BIT)

GCC := g++
CFLAGS = -g -Wall -I$(VINC) -I $(VDIRFB)
#
# Modern versions of Verilator and C++ may require an -faligned-new flag
# CFLAGS = -g -Wall -faligned-new -I$(VINC) -I $(VDIRFB)

VERILATOR=verilator
VFLAGS := -O3 -MMD --trace -Wall

## Find the directory containing the Verilog sources.  This is given from
## calling: "verilator -V" and finding the VERILATOR_ROOT output line from
## within it.  From this VERILATOR_ROOT value, we can find all the components
## we need here--in particular, the verilator include directory
VERILATOR_ROOT ?= $(shell bash -c '$(VERILATOR) -V|grep VERILATOR_ROOT | head -1 | sed -e "s/^.*=\s*//"')
##
## The directory containing the verilator includes
VINC := $(VERILATOR_ROOT)/include

$(VDIRFB)/V$(TOPMOD).cpp: $(VLOGFIL)
	$(VERILATOR) $(VFLAGS) -cc $(VLOGFIL)

$(VDIRFB)/V$(TOPMOD)__ALL.a: $(VDIRFB)/V$(TOPMOD).cpp
	make --no-print-directory -C $(VDIRFB) -f V$(TOPMOD).mk

$(SIMPROG): $(SIMFILE) $(VDIRFB)/V$(TOPMOD)__ALL.a $(COSIMS)
	$(GCC) $(CFLAGS) $(VINC)/verilated.cpp				\
		$(VINC)/verilated_vcd_c.cpp $(SIMFILE) $(COSIMS)	\
		$(VDIRFB)/V$(TOPMOD)__ALL.a -o $(SIMPROG)

test: $(VCDFILE)

$(VCDFILE): $(SIMPROG)
	./$(SIMPROG)

##
.PHONY: clean
clean:
	rm -rf $(VDIRFB)/ $(SIMPROG) $(VCDFILE) top/ $(BINFILE) $(RPTFILE)
	rm -rf $(ULX3S_BUILD_DIR)

##
## Find all of the Verilog dependencies and submodules
##
DEPS := $(wildcard $(VDIRFB)/*.d)

## Include any of these submodules in the Makefile
## ... but only if we are not building the "clean" target
## which would (oops) try to build those dependencies again
##
ifneq ($(MAKECMDGOALS),clean)
ifneq ($(DEPS),)
include $(DEPS)
endif
endif


$(ULX3S_BUILD_DIR):
	mkdir -p $(ULX3S_BUILD_DIR)

$(ULX3S_BIT): $(ULX3S_CONFIG) | $(ULX3S_BUILD_DIR)
	ecppack $(ULX3S_CONFIG) $(ULX3S_BIT)

$(ULX3S_CONFIG): $(ULX3S_JSON) $(ULX3S_LPF) | $(ULX3S_BUILD_DIR)
	nextpnr-ecp5 --25k --json $(ULX3S_JSON) --lpf $(ULX3S_LPF) --textcfg $(ULX3S_CONFIG)

$(ULX3S_JSON): $(VLOGFIL) | $(ULX3S_BUILD_DIR)
	yosys -ql $(ULX3S_LOG) -p 'read_verilog $(VLOGFIL); synth_ecp5 -json $(ULX3S_JSON)'

prog: $(ULX3S_BIT)
	/mnt/c/workspace/ulx3s-examples/bin/ujprog.exe $(ULX3S_BIT)
