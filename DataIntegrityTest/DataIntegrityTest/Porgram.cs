using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DataIntegrityTest
{
    // Define a structured data class for testing
    class DataItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public List<string> Tags { get; set; }
        
        // Create a checksum of the data for integrity verification
        public string GetChecksum()
        {
            string data = $"{Id}|{Name}|{Value}|{Timestamp.Ticks}|{string.Join(",", Tags)}";
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(bytes);
            }
        }
        
        // Method to create a test item with random data
        public static DataItem CreateTestItem(int id)
        {
            Random random = new Random(id); // Seed with id for reproducibility
            string[] possibleTags = { "test", "important", "low", "high", "medium", "critical", "normal" };
            
            return new DataItem
            {
                Id = id,
                Name = $"Item-{id:D4}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Value = Math.Round(random.NextDouble() * 1000, 2),
                Timestamp = DateTime.Now,
                Tags = Enumerable.Range(0, random.Next(1, 4))
                    .Select(_ => possibleTags[random.Next(possibleTags.Length)])
                    .ToList()
            };
        }
    }
    
    class Program
    {
        private const int Port = 8080;
        private const string EndMarker = "DONE";
        private const int NumItems = 10;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please specify 'producer' or 'consumer' as a command line argument.");
                return;
            }

            string mode = args[0].ToLower();

            if (mode == "producer")
            {
                RunProducer();
            }
            else if (mode == "consumer")
            {
                RunConsumer();
            }
            else
            {
                Console.WriteLine("Invalid mode. Please specify 'producer' or 'consumer'.");
            }
        }

        static void RunProducer()
        {
            Console.WriteLine("=== Producer Starting - Data Integrity Test ===");
            Console.WriteLine($"Will generate {NumItems} structured data items");

            // Create test data
            List<DataItem> testItems = new List<DataItem>();
            for (int i = 1; i <= NumItems; i++)
            {
                testItems.Add(DataItem.CreateTestItem(i));
            }
            
            // Create a log of checksums for verification
            List<string> sentChecksums = new List<string>();
            foreach (var item in testItems)
            {
                sentChecksums.Add(item.GetChecksum());
            }
            
            // Write checksums to file for verification
            File.WriteAllLines("sent_checksums.txt", sentChecksums);
            Console.WriteLine($"Generated {testItems.Count} data items with checksums saved to sent_checksums.txt");

            // Create a TCP/IP socket
            TcpListener listener = new TcpListener(IPAddress.Loopback, Port);
            
            try
            {
                // Start listening for client connections
                listener.Start();
                Console.WriteLine($"Producer listening on port {Port}");
                Console.WriteLine("Waiting for consumer to connect...");

                // Accept a client connection
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    Console.WriteLine("Consumer connected!");

                    // Send structured data items
                    foreach (var item in testItems)
                    {
                        // Serialize to JSON
                        string json = JsonSerializer.Serialize(item);
                        
                        // Send the data
                        writer.WriteLine(json);
                        Console.WriteLine($"Produced Item {item.Id}: Name={item.Name}, Value={item.Value}");
                        Console.WriteLine($"Checksum: {item.GetChecksum()}");
                        
                        // Simulate production time
                        Thread.Sleep(1000); // 1 second
                    }

                    // Send termination signal
                    writer.WriteLine(EndMarker);
                    Console.WriteLine($"Sent termination signal: {EndMarker}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Producer Error: {ex.Message}");
            }
            finally
            {
                // Stop listening
                listener.Stop();
                Console.WriteLine("=== Producer Completed ===");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        static void RunConsumer()
        {
            Console.WriteLine("=== Consumer Starting - Data Integrity Test ===");
            Console.WriteLine("Connecting to producer...");
            
            // Store received checksums for verification
            List<string> receivedChecksums = new List<string>();
            
            try
            {
                // Create a TCP/IP client
                using (TcpClient client = new TcpClient())
                {
                    // Connect to the producer
                    IAsyncResult connectResult = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                    
                    // Set timeout
                    bool success = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                    
                    if (!success)
                    {
                        Console.WriteLine("Failed to connect to producer (timeout)");
                        return;
                    }
                    
                    // Complete the connection
                    client.EndConnect(connectResult);
                    
                    Console.WriteLine("Connected to producer!");
                    
                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string line;
                        int itemCount = 0;
                        
                        // Process incoming data
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Check for termination signal
                            if (line == EndMarker)
                            {
                                Console.WriteLine("Received termination signal");
                                break;
                            }

                            try
                            {
                                // Deserialize the JSON
                                DataItem item = JsonSerializer.Deserialize<DataItem>(line);
                                
                                // Compute checksum of received data
                                string checksum = item.GetChecksum();
                                receivedChecksums.Add(checksum);
                                
                                // Process the item
                                itemCount++;
                                Console.WriteLine($"Consumed Item {item.Id}: Name={item.Name}, Value={item.Value}");
                                Console.WriteLine($"Received Checksum: {checksum}");
                                
                                // Simulate processing time
                                Thread.Sleep(1500); // 1.5 seconds
                            }
                            catch (JsonException ex)
                            {
                                Console.WriteLine($"Error deserializing data: {ex.Message}");
                                Console.WriteLine($"Received invalid data: {line}");
                            }
                        }
                        
                        Console.WriteLine($"Processed {itemCount} items total");
                    }
                }
                
                // Write received checksums to file
                File.WriteAllLines("received_checksums.txt", receivedChecksums);
                Console.WriteLine("Checksums saved to received_checksums.txt");
                
                // Verify data integrity if producer's checksums are available
                if (File.Exists("sent_checksums.txt"))
                {
                    string[] sentChecksums = File.ReadAllLines("sent_checksums.txt");
                    VerifyDataIntegrity(sentChecksums, receivedChecksums.ToArray());
                }
                else
                {
                    Console.WriteLine("Producer's checksums file not found. Run the test on the same machine for verification.");
                }
                
                Console.WriteLine("=== Consumer Completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Consumer Error: {ex.Message}");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
        
        static void VerifyDataIntegrity(string[] sentChecksums, string[] receivedChecksums)
        {
            Console.WriteLine("\n=== Data Integrity Verification ===");
            
            if (sentChecksums.Length != receivedChecksums.Length)
            {
                Console.WriteLine($"INTEGRITY ERROR: Item count mismatch! Sent: {sentChecksums.Length}, Received: {receivedChecksums.Length}");
                return;
            }
            
            int matchCount = 0;
            int errorCount = 0;
            
            for (int i = 0; i < sentChecksums.Length; i++)
            {
                if (sentChecksums[i] == receivedChecksums[i])
                {
                    matchCount++;
                }
                else
                {
                    errorCount++;
                    Console.WriteLine($"INTEGRITY ERROR: Item {i+1} checksums do not match!");
                    Console.WriteLine($"  Sent:     {sentChecksums[i]}");
                    Console.WriteLine($"  Received: {receivedChecksums[i]}");
                }
            }
            
            Console.WriteLine($"Total items: {sentChecksums.Length}");
            Console.WriteLine($"Matching items: {matchCount}");
            Console.WriteLine($"Corrupted items: {errorCount}");
            
            if (errorCount == 0)
            {
                Console.WriteLine("DATA INTEGRITY VERIFICATION: SUCCESS - All items transmitted correctly!");
            }
            else
            {
                Console.WriteLine("DATA INTEGRITY VERIFICATION: FAILED - Some items were corrupted during transmission!");
            }
            
            Console.WriteLine("=== End of Verification ===\n");
        }
    }
}
