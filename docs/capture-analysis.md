# Sanitized Protocol Analysis

This document records only verified implementation facts needed for the current device save and restore preview.

## Write Page Integrity

- A generated write page is 62 bytes long.
- Its CRC is CRC-16 with initial value `0xFFFF` and reflected polynomial `0xA001`.
- The CRC covers bytes 17-61 inclusive, the 45-byte page payload.
- The CRC is stored in bytes 7-8 in little-endian order: low byte first, then high byte.
- The page offset in bytes 13-16 is also little-endian.

## Save And Restore Safety

- A save first reads the active configuration from the connected device. It starts from that baseline and changes only the selected mapping bytes, preserving every other byte through read-modify-write.
- The baseline is backed up to `Documents/MicroKeyStudio/backups` before the user is asked to confirm the save.
- After a save or restore, the app reads the complete configuration again. An incomplete readback or a requested mapping mismatch does not report save success. Restore requires the complete readback to match the backup byte-for-byte.

## Current Limitation And Verification Boundary

- The protocol supports only the active configuration of the connected device. Device-side profile names and profile selection have not been decoded.
- Automated tests verify packet construction, byte preservation, backup ordering, and simulated complete readback handling. Hardware save and restore have not been independently verified; that physical-device validation is pending Task 9.
