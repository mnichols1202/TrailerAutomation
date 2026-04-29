#ifndef OTAUPDATE_H
#define OTAUPDATE_H

/**
 * Download firmware binary from url and flash it.
 * On success the device reboots automatically and this function never returns.
 * On failure returns false and the device keeps running.
 */
bool performOtaUpdate(const char* url);

#endif // OTAUPDATE_H
