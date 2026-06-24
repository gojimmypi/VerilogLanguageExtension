# SPI

The `spi_slave.v` implements a small [Serial Peripheral Interface](https://en.wikipedia.org/wiki/Serial_Peripheral_Interface) 
(SPI) Mode 0, MSB-first slave for register access in the Tiny Tapeout UART/FSM/TRNG Lab project. 

It shares the SPI/JTAG pins and supports simple two-byte transactions: 

a command byte followed by either a dummy byte for reads or a data byte for writes. 

The command selects registers 0 through 7, with bit 7 indicating read versus write. 

Writable registers control TRNG behavior, while status and raw TRNG registers are read-only. 

An optional `SPI_TEST_FIXED` build mode macro keeps the earlier fixed-byte transmit test path for bring-up/debug.


## Example

See the [`/ulx3s/ESP32`](../../ulx3s/ESP32/) example application.

## Other SPI to consider

See a [thread on Discord](https://discord.com/channels/1009193568256135208/1515961612928946326/1516102881571373066) that mentions:

>  `@caioalonso`'s SPI and I2C IP ... already silicon proven and works well

https://github.com/calonso88/tt_spi_i2c_reg_bank/tree/main

