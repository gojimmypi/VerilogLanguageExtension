PROJ = top_icebreaker
BUILD_DIR ?= build
ICEBREAKER_BUILD_DIR := $(BUILD_DIR)/icebreaker
ICEBREAKER_JSON := $(ICEBREAKER_BUILD_DIR)/$(PROJ).json
ICEBREAKER_ASC := $(ICEBREAKER_BUILD_DIR)/$(PROJ).asc
ICEBREAKER_BIN := $(ICEBREAKER_BUILD_DIR)/$(PROJ).bin
ICEBREAKER_RPT := $(ICEBREAKER_BUILD_DIR)/$(PROJ).rpt
ICEBREAKER_YSLOG := $(ICEBREAKER_BUILD_DIR)/$(PROJ).yslog
ICEBREAKER_NPLOG := $(ICEBREAKER_BUILD_DIR)/$(PROJ).nplog
ICEBREAKER_PCF := boards/icebreaker/icebreaker.pcf

all: $(ICEBREAKER_RPT) $(ICEBREAKER_BIN)

$(ICEBREAKER_BUILD_DIR):
	mkdir -p $(ICEBREAKER_BUILD_DIR)

$(ICEBREAKER_JSON): $(PROJ).v | $(ICEBREAKER_BUILD_DIR)
	yosys -ql $(ICEBREAKER_YSLOG) -p 'synth_ice40 -top top_icebreaker -json $@' $<

$(ICEBREAKER_ASC): $(ICEBREAKER_JSON) $(ICEBREAKER_PCF) | $(ICEBREAKER_BUILD_DIR)
	nextpnr-ice40 -ql $(ICEBREAKER_NPLOG) --up5k --package sg48 --freq 12 --asc $@ --pcf $(ICEBREAKER_PCF) --json $<

$(ICEBREAKER_BIN): $(ICEBREAKER_ASC) | $(ICEBREAKER_BUILD_DIR)
	icepack $< $@

$(ICEBREAKER_RPT): $(ICEBREAKER_ASC) | $(ICEBREAKER_BUILD_DIR)
	icetime -d up5k -c 12 -mtr $@ $<

$(PROJ)_tb: $(PROJ)_tb.v $(PROJ).v
	iverilog -o $@ $^

$(PROJ)_tb.vcd: $(PROJ)_tb
	vvp -N $< +vcd=$@

$(PROJ)_syn.v: $(ICEBREAKER_JSON)
	yosys -p 'read_json $^; write_verilog $@'

$(PROJ)_syntb: $(PROJ)_tb.v $(PROJ)_syn.v
	iverilog -o $@ $^ `yosys-config --datdir/ice40/cells_sim.v`

$(PROJ)_syntb.vcd: $(PROJ)_syntb
	vvp -N $< +vcd=$@

prog: $(ICEBREAKER_BIN)
	iceprog $<

sudo-prog: $(ICEBREAKER_BIN)
	@echo 'Executing prog as root!!!'
	sudo iceprog $<

clean:
	rm -rf $(ICEBREAKER_BUILD_DIR)
	rm -f $(PROJ)_tb $(PROJ)_tb.vcd $(PROJ)_syn.v $(PROJ)_syntb $(PROJ)_syntb.vcd

.SECONDARY:
.PHONY: all prog clean
