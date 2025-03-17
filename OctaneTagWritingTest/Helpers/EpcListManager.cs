﻿using EpcListGenerator;
using OctaneTagWritingTest.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class EpcListManager
{
    // Singleton instance with lazy initialization (thread-safe)
    private static readonly Lazy<EpcListManager> instance =
        new Lazy<EpcListManager>(() => new EpcListManager());

    /// <summary>
    /// Gets the singleton instance of the EpcListManager.
    /// </summary>
    public static EpcListManager Instance => instance.Value;

    // Instance fields
    private Queue<string> epcQueue = new Queue<string>();
    private readonly object lockObj = new object();
    private string lastEpc = "000000000000000000000000";
    private long currentSerialNumber = 1;
    private string epcHeader = "B200";
    private string epcPlainItemCode = "99999999999999";
    private long quantity = 1;

    // Thread-safe dictionary to ensure unique EPC generation using tag TID as key.
    private ConcurrentDictionary<string, string> generatedEpcsByTid = new ConcurrentDictionary<string, string>();

    // Private constructor to prevent external instantiation.
    private EpcListManager()
    {
    }

    /// <summary>
    /// Loads the EPC list from the specified file.
    /// </summary>
    /// <param name="filePath">The path to the EPC file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the EPC file is not found.</exception>
    public void LoadEpcList(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("EPC file not found.", filePath);

        lock (lockObj)
        {
            epcQueue.Clear();
            string[] lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                string epc = line.Trim();
                if (!string.IsNullOrEmpty(epc))
                    epcQueue.Enqueue(epc);
            }

            if (epcQueue.Any())
                lastEpc = epcQueue.Last();
        }
    }

    /// <summary>
    /// Initializes EPC data with the specified header, code, and quantity.
    /// </summary>
    /// <param name="header">The EPC header.</param>
    /// <param name="code">The plain item code.</param>
    /// <param name="epcQuantity">The number of EPCs to generate (default is 1).</param>
    public void InitEpcData(string header, string code, long epcQuantity = 1)
    {
        epcHeader = header;
        epcPlainItemCode = code;
        quantity = epcQuantity;
    }

    /// <summary>
    /// Gets the next unique EPC.
    /// If a TID is provided, uses a thread-safe dictionary to ensure uniqueness.
    /// If the TID is null or empty, the current generation logic is used.
    /// </summary>
    /// <param name="tid">The tag TID used as a key for uniqueness (optional).</param>
    /// <returns>The next unique EPC string.</returns>
    public string GetNextEpc(string tid = null)
    {
        if (!string.IsNullOrEmpty(tid))
        {
            // If TID is provided, use the dictionary to guarantee uniqueness.
            // If an EPC for this TID hasn't been generated, GenerateUniqueEpc is called and the value is added.
            return generatedEpcsByTid.GetOrAdd(tid, key => GenerateUniqueEpc());
        }
        else
        {
            // If TID is null or empty, use the current logic.
            return GenerateUniqueEpc();
        }
    }

    /// <summary>
    /// Generates a unique EPC based on the current serial number.
    /// Ensures thread safety during EPC generation.
    /// </summary>
    /// <returns>A unique EPC string.</returns>
    private string GenerateUniqueEpc()
    {
        lock (lockObj)
        {
            // Generate the EPC list using the current serial number.
            List<string> createdEpcs = EpcListGeneratorHelper.Instance.GenerateCustomEpcList(
                epcHeader, epcPlainItemCode, quantity, currentSerialNumber);

            string currentEpc = createdEpcs.FirstOrDefault();
            currentSerialNumber++;

            // If the generated EPC already exists, generate a new EPC with the next serial number.
            if (TagOpController.Instance.GetExistingEpc(currentEpc))
            {
                createdEpcs = EpcListGeneratorHelper.Instance.GenerateCustomEpcList(
                    epcHeader, epcPlainItemCode, quantity, currentSerialNumber);
                currentEpc = createdEpcs.FirstOrDefault();
                currentSerialNumber++;
            }

            Console.WriteLine($"Returning next EPC created: {currentEpc}: SN = {currentSerialNumber}");
            return currentEpc;
        }
    }

    /// <summary>
    /// Generates a new serial number based on the last EPC.
    /// </summary>
    /// <returns>A new serial number string.</returns>
    private string GenerateNewSerialNumber()
    {
        string prefix = lastEpc.Substring(0, lastEpc.Length - 6);
        string lastDigits = lastEpc.Substring(lastEpc.Length - 6);

        int number = int.Parse(lastDigits, System.Globalization.NumberStyles.HexNumber);
        number++;

        return prefix + number.ToString("X4");
    }

    /// <summary>
    /// Loads the TID list from the specified file.
    /// </summary>
    /// <param name="filePath">The path to the TID list file.</param>
    /// <returns>A list of TID strings.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the TID file is not found.</exception>
    public List<string> LoadTidList(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("TID list file not found.", filePath);

        var tidList = new List<string>();

        lock (lockObj)
        {
            string[] lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                string tid = line.Trim();
                if (!string.IsNullOrEmpty(tid))
                    tidList.Add(tid);
            }
            return tidList;
        }
    }
}
