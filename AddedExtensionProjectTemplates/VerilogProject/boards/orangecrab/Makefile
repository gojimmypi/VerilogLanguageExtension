PROJ := blink
BOARD_DIR := boards/orangecrab
BUILD_DIR ?= build
ORANGECRAB_BUILD_DIR := $(BUILD_DIR)/orangecrab

# r0.1 or r0.2
VERSION := r0.2

ORANGECRAB_JSON := $(ORANGECRAB_BUILD_DIR)/$(PROJ).json
ORANGECRAB_CONFIG := $(ORANGECRAB_BUILD_DIR)/$(PROJ)_out.config
ORANGECRAB_BIT := $(ORANGECRAB_BUILD_DIR)/$(PROJ).bit
ORANGECRAB_DFU := $(ORANGECRAB_BUILD_DIR)/$(PROJ).dfu
ORANGECRAB_PCF := $(BOARD_DIR)/orangecrab_$(VERSION).pcf
ORANGECRAB_SRC := $(BOARD_DIR)/$(PROJ).v

all: $(ORANGECRAB_DFU)

dfu: $(ORANGECRAB_DFU)
	dfu-util -D $<

$(ORANGECRAB_BUILD_DIR):
	mkdir -p $(ORANGECRAB_BUILD_DIR)

$(ORANGECRAB_JSON): $(ORANGECRAB_SRC) | $(ORANGECRAB_BUILD_DIR)
	yosys -p "synth_ecp5 -json $@" $<

$(ORANGECRAB_CONFIG): $(ORANGECRAB_JSON) $(ORANGECRAB_PCF) | $(ORANGECRAB_BUILD_DIR)
	nextpnr-ecp5 --json $< --textcfg $@ --25k --package CSFBGA285 --lpf $(ORANGECRAB_PCF)

$(ORANGECRAB_BIT): $(ORANGECRAB_CONFIG) | $(ORANGECRAB_BUILD_DIR)
	ecppack --compress --freq 38.8 --input $< --bit $@

$(ORANGECRAB_DFU): $(ORANGECRAB_BIT) | $(ORANGECRAB_BUILD_DIR)
	cp $< $@
	dfu-suffix -v 1209 -p 5af0 -a $@

clean:
	rm -rf $(ORANGECRAB_BUILD_DIR)

.PHONY: all dfu clean
