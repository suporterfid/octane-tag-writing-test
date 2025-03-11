using System;
using System.Collections.Generic;
using System.IO;
using Impinj.TagUtils;

namespace EpcListGenerator
{
    public static class EpcListGeneratorHelper
    {
        /// <summary>
        /// Generates a list of EPC strings based on the provided GTIN and quantity.
        /// </summary>
        /// <param name="gtin">The GTIN string. If null or empty, a default value is used.</param>
        /// <param name="quantity">The number of EPCs to generate. Must be less than 10^10.</param>
        /// <returns>A list of EPC strings.</returns>
        /// <exception cref="ArgumentException">Thrown when quantity is too high.</exception>
        public static List<string> GenerateEncodedEpcList(string gtin, long quantity)
        {
            // Use default GTIN if none provided.
            if (string.IsNullOrWhiteSpace(gtin))
            {
                gtin = "07891033079360";
            }

            // Validate the quantity.
            if (quantity >= 10000000000)
            {
                throw new ArgumentException(
                    "Quantity too high: the serial number should have up to 10 digits. Provide a quantity less than 10^10.",
                    nameof(quantity));
            }

            var epcList = new List<string>();

            // Generate EPCs sequentially.
            for (long i = 1; i <= quantity; i++)
            {
                // Create the Sgtin96 object using the provided GTIN and a partition value of 6.
                var sgtin96 = Sgtin96.FromGTIN(gtin, 6);
                sgtin96.SerialNumber = (ulong)i;
                epcList.Add(sgtin96.ToEpc());
            }

            return epcList;
        }

        /// <summary>
        /// Generates a custom list of EPC strings based on a 4-character header, a 14-character middle part, and a quantity.
        /// The resulting EPCs are 24 characters long, where the last 6 digits form a unique serial number.
        /// </summary>
        /// <param name="epcHeader">A 4-character string representing the EPC header (e.g. "B071").</param>
        /// <param name="middlePart">A 14-character string that forms the middle part of the EPC.</param>
        /// <param name="quantity">The number of EPCs to generate. Must be less than or equal to 999999.</param>
        /// <returns>A list of 24-character EPC strings.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when epcHeader is not 4 characters,
        /// or middlePart is not 14 characters,
        /// or quantity is greater than 999999.
        /// </exception>
        public static List<string> GenerateCustomEpcList(string epcHeader, string middlePart, long quantity, long initSerial)
        {
            // Validate epcHeader is exactly 4 characters.
            if (string.IsNullOrWhiteSpace(epcHeader) || epcHeader.Length != 4)
            {
                throw new ArgumentException("EPC header must be exactly 4 characters long.", nameof(epcHeader));
            }

            // Validate middlePart is exactly 14 characters.
            if (string.IsNullOrWhiteSpace(middlePart) || middlePart.Length != 14)
            {
                throw new ArgumentException("The middle part must be exactly 14 characters long.", nameof(middlePart));
            }

            // Validate that quantity does not exceed the maximum unique serial numbers available (6 digits).
            if (quantity > 999999)
            {
                throw new ArgumentException("Quantity too high: the unique serial number must be 6 digits. Provide a quantity of 999999 or less.", nameof(quantity));
            }

            var epcList = new List<string>();

            // Generate EPCs sequentially.
            for (long i = 1; i <= quantity; i++)
            {
                // Generate a unique 6-digit serial number with leading zeros.
                string serial = initSerial.ToString("D6");
                string epc = epcHeader + middlePart + serial;


                // Ensure the resulting EPC is 24 characters long.
                if (epc.Length != 24)
                {
                    throw new InvalidOperationException("The generated EPC must be 24 characters long.");
                }

                epcList.Add(epc);
                initSerial = initSerial + 1;
            }

            return epcList;
        }

        /// <summary>
        /// Saves the EPC list to a file.
        /// </summary>
        /// <param name="epcList">The list of EPC strings to save.</param>
        /// <param name="outputFilePath">The path of the output file.</param>
        public static void SaveEpcListToFile(List<string> epcList, string outputFilePath)
        {
            File.WriteAllLines(outputFilePath, epcList);
        }
    }
}
