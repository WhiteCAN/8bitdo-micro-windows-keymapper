# Protocol Notes

This document contains sanitized protocol observations for MicroKey Studio.

## Observed BLE UUIDs

- Service UUID: `0000FF10-0000-1000-8000-00805F9B34FB`
- Write/notify characteristic UUID: `0000FF13-0000-1000-8000-00805F9B34FB`

## Observed App Flow

1. The official app discovers the Micro in BLE app mode.
2. It connects and configures the BLE session.
3. It resolves the Micro service and characteristic.
4. It subscribes to notifications.
5. It sends report-enable and configuration packets.
6. It sends save or commit packets.

## Notes

- The complete configuration is read as four 45-byte pages at offsets `0x00`, `0x2D`, `0x5A`, and `0x87`.
- Three official-mobile-app captures covered Disable Sleep OFF → ON → OFF.
- The two OFF captures had byte-identical 180-byte configurations.
- The only ON/OFF configuration difference is global offset `0x03`: ON=`0x01`, OFF=`0x00`.
- If offset `0x03` is not the confirmed `0x00/0x01`, the app neither interprets nor normalizes it and preserves the original byte.
- The response-page CRC changes with the value, and generated write pages recalculate CRC16 for the modified 45-byte page.
- A save freshly reads the complete configuration, modifies only confirmed fields, and verifies requested values through another complete readback.
