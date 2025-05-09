using System;
using System.Globalization;
using Unity.Mathematics;

namespace Elfenlabs.String
{
    public static class FormatUtility
    {
        // Define unit suffixes and their corresponding byte thresholds (using 1024 for KiB, MiB etc.)
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        private const long BytesPerKilobyte = 1024;

        /// <summary>
        /// Formats a byte size into a human-readable string (e.g., "123 B", "45.6 KB", "1.23 MB")
        /// with a maximum of 3 digits shown before the decimal point for KB and higher units.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <returns>A formatted string representation of the byte size.</returns>
        public static string FormatBytes(long bytes, string separator = " ")
        {
            if (bytes < 0) { return "-" + FormatBytes(-bytes); } // Handle negative values if needed
            if (bytes == 0) { return $"0{separator}B"; }

            // Determine the magnitude and suffix
            int magnitude = (int)Math.Log(bytes, BytesPerKilobyte);

            // Ensure magnitude is within the bounds of our suffixes array
            magnitude = Math.Min(magnitude, SizeSuffixes.Length - 1);

            // Calculate the value in the chosen magnitude
            double adjustedSize = (double)bytes / Math.Pow(BytesPerKilobyte, magnitude);

            // Format the number based on its size to keep max 3 digits before the decimal
            string format;
            if (magnitude == 0) // Bytes
            {
                format = "N0"; // No decimal places for Bytes
            }
            else if (adjustedSize < 10.0)
            {
                format = "N2"; // 1.23 KB/MB/GB...
            }
            else if (adjustedSize < 100.0)
            {
                format = "N1"; // 12.3 KB/MB/GB...
            }
            else // 100.0 to 1023.9...
            {
                format = "N0"; // 123 KB/MB/GB... (no decimal needed)
            }

            // Use InvariantCulture for consistent decimal point formatting
            return adjustedSize.ToString(format, CultureInfo.InvariantCulture) + " " + SizeSuffixes[magnitude];
        }
    }
}