#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* Clean-room fixture for the five-byte form accepted by Unity's compressed
 * integer reader. It intentionally shares no implementation with the C# codec. */
static int hex_nibble(char c)
{
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    return -1;
}

static uint32_t read_five_byte_uint(const char *hex)
{
    uint8_t bytes[5];
    if (strlen(hex) != 10) exit(10);
    for (int i = 0; i < 5; ++i)
    {
        int high = hex_nibble(hex[i * 2]);
        int low = hex_nibble(hex[i * 2 + 1]);
        if (high < 0 || low < 0) exit(11);
        bytes[i] = (uint8_t)((high << 4) | low);
    }
    if (bytes[0] != 0xF0) exit(12);
    return (uint32_t)bytes[1] | ((uint32_t)bytes[2] << 8) |
        ((uint32_t)bytes[3] << 16) | ((uint32_t)bytes[4] << 24);
}

static int32_t decode_signed(uint32_t encoded)
{
    if (encoded == UINT32_MAX) return INT32_MIN;
    return (encoded & 1) ? -(int32_t)((encoded >> 1) + 1) : (int32_t)(encoded >> 1);
}

int main(void)
{
    char line[128];
    unsigned count = 0;
    while (fgets(line, sizeof line, stdin))
    {
        char kind = 0, value[32], hex[16];
        if (sscanf(line, "%c\t%31[^\t]\t%15s", &kind, value, hex) != 3) return 20;
        uint32_t encoded = read_five_byte_uint(hex);
        if (kind == 'i')
        {
            char *end;
            intmax_t expected = strtoimax(value, &end, 10);
            if (*end || expected != decode_signed(encoded)) return 21;
        }
        else if (kind == 'u')
        {
            char *end;
            uintmax_t expected = strtoumax(value, &end, 10);
            if (*end || expected != encoded) return 22;
        }
        else return 23;
        ++count;
    }
    if (count != 10) return 24;
    printf("PASS: native reader decoded %u managed vectors\n", count);
    return 0;
}
