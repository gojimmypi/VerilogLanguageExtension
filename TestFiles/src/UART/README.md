# UART

Implements a simple [Universal asynchronous receiver-transmitter](https://en.wikipedia.org/wiki/Universal_asynchronous_receiver-transmitter) (UART)
to control and view registers.

The UART is normally enabled. See the `UART_ENABLED` macro in `project_config.v` along with the related `PROJECT_UART_BAUD` and `PROJECT_CLOCK_HZ`.

Runtime adjustable baud rates can be enables with the `ADJUSTABLE_BAUD_ENABLED` macro.
