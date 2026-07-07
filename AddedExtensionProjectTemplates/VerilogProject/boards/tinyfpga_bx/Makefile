# Makefile borrowed from https://github.com/cliffordwolf/icestorm/blob/master/examples/icestick/Makefile
#
# The following license is from the icestorm project and specifically applies to this file only:
#
#  Permission to use, copy, modify, and/or distribute this software for any
#  purpose with or without fee is hereby granted, provided that the above
#  copyright notice and this permission notice appear in all copies.
#
#  THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
#  WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
#  MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
#  ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
#  WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
#  ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
#  OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

PROJ = TinyFPGA_B
BOARD_DIR := boards/tinyfpga_bx
BUILD_DIR ?= build
TINYFPGA_BUILD_DIR := $(BUILD_DIR)/tinyfpga-bx

PIN_DEF := $(BOARD_DIR)/pins.pcf
VLOG_SRC := $(BOARD_DIR)/$(PROJ).v
DEVICE := lp8k
PACKAGE := cm81
FREQ ?= 12

TINYFPGA_JSON := $(TINYFPGA_BUILD_DIR)/$(PROJ).json
TINYFPGA_ASC := $(TINYFPGA_BUILD_DIR)/$(PROJ).asc
TINYFPGA_BIN := $(TINYFPGA_BUILD_DIR)/$(PROJ).bin
TINYFPGA_RPT := $(TINYFPGA_BUILD_DIR)/$(PROJ).rpt
TINYFPGA_YSLOG := $(TINYFPGA_BUILD_DIR)/$(PROJ).yslog
TINYFPGA_NPLOG := $(TINYFPGA_BUILD_DIR)/$(PROJ).nplog

all: $(TINYFPGA_RPT) $(TINYFPGA_BIN)

$(TINYFPGA_BUILD_DIR):
	mkdir -p $(TINYFPGA_BUILD_DIR)

$(TINYFPGA_JSON): $(VLOG_SRC) | $(TINYFPGA_BUILD_DIR)
	yosys -ql $(TINYFPGA_YSLOG) -p 'synth_ice40 -top $(PROJ) -json $@' $<

$(TINYFPGA_ASC): $(PIN_DEF) $(TINYFPGA_JSON) | $(TINYFPGA_BUILD_DIR)
	nextpnr-ice40 -ql $(TINYFPGA_NPLOG) --lp8k --package $(PACKAGE) --freq $(FREQ) --asc $@ --pcf $(PIN_DEF) --json $(TINYFPGA_JSON)

$(TINYFPGA_BIN): $(TINYFPGA_ASC) | $(TINYFPGA_BUILD_DIR)
	icepack $< $@

$(TINYFPGA_RPT): $(TINYFPGA_ASC) | $(TINYFPGA_BUILD_DIR)
	icetime -d $(DEVICE) -c $(FREQ) -mtr $@ $<

$(PROJ)_tb: $(BOARD_DIR)/$(PROJ)_tb.v $(VLOG_SRC)
	iverilog -o $@ $^

$(PROJ)_tb.vcd: $(PROJ)_tb
	vvp -N $< +vcd=$@

$(PROJ)_syn.v: $(TINYFPGA_JSON)
	yosys -p 'read_json $^; write_verilog $@'

$(PROJ)_syntb: $(BOARD_DIR)/$(PROJ)_tb.v $(PROJ)_syn.v
	iverilog -o $@ $^ `yosys-config --datdir/ice40/cells_sim.v`

$(PROJ)_syntb.vcd: $(PROJ)_syntb
	vvp -N $< +vcd=$@

prog: $(TINYFPGA_BIN)
	tinyprog -p $<

sudo-prog: $(TINYFPGA_BIN)
	@echo 'Executing prog as root!!!'
	sudo tinyprog -p $<

clean:
	rm -rf $(TINYFPGA_BUILD_DIR)
	rm -f $(PROJ)_tb $(PROJ)_tb.vcd $(PROJ)_syn.v $(PROJ)_syntb $(PROJ)_syntb.vcd

.SECONDARY:
.PHONY: all prog sudo-prog clean
