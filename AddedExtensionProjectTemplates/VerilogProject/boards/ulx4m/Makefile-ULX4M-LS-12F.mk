.PHONY: all clean prog
.DELETE_ON_ERROR:
BUILD_DIR ?= build
ULX4M_BUILD_DIR := $(BUILD_DIR)/ulx4m-ls-12k
ULX4M_TOP := top_ulx4m
ULX4M_VERILOG := boards/ulx4m/top_ulx4m.v
ULX4M_LPF := boards/ulx4m/ulx4m_minimal.lpf
ULX4M_JSON := $(ULX4M_BUILD_DIR)/ulx4m.json
ULX4M_CONFIG := $(ULX4M_BUILD_DIR)/ulx4m_out.config
ULX4M_BIT := $(ULX4M_BUILD_DIR)/ulx4m.bit
ULX4M_LOG := $(ULX4M_BUILD_DIR)/ulx4m.yslog

all: $(ULX4M_BIT)

$(ULX4M_BUILD_DIR):
	mkdir -p $(ULX4M_BUILD_DIR)

$(ULX4M_BIT): $(ULX4M_CONFIG) | $(ULX4M_BUILD_DIR)
	ecppack $(ULX4M_CONFIG) $(ULX4M_BIT)

$(ULX4M_CONFIG): $(ULX4M_JSON) $(ULX4M_LPF) | $(ULX4M_BUILD_DIR)
	nextpnr-ecp5 --12k --package CABGA381 --json $(ULX4M_JSON) --lpf $(ULX4M_LPF) --textcfg $(ULX4M_CONFIG)

$(ULX4M_JSON): $(ULX4M_VERILOG) | $(ULX4M_BUILD_DIR)
	yosys -ql $(ULX4M_LOG) -p 'read_verilog $(ULX4M_VERILOG); synth_ecp5 -top $(ULX4M_TOP) -json $(ULX4M_JSON)'

clean:
	rm -rf $(ULX4M_BUILD_DIR)

prog: $(ULX4M_BIT)
	fujprog $(ULX4M_BIT)
