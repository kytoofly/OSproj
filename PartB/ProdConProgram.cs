using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace IPC
{
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
            Console.WriteLine("=== Producer Starting ===");

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
                    Random random = new Random();

                    // Generate and send random numbers
                    for (int i = 1; i <= NumItems; i++)
                    {
                        // Generate a random number between 1 and 100
                        int data = random.Next(1, 101);
                        
                        // Send the data
                        writer.WriteLine(data);
                        Console.WriteLine($"Produced: {data}");
                        
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
            Console.WriteLine("=== Consumer Starting ===");
            Console.WriteLine("Connecting to producer...");
            
            try
            {
                // Create a TCP/IP client
                using (TcpClient client = new TcpClient())
                {
                    // Connect to the producer
                    IAsyncResult result = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                    
                    // Set timeout
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                    
                    if (!success)
                    {
                        Console.WriteLine("Failed to connect to producer (timeout)");
                        return;
                    }
                    
                    // Complete the connection
                    client.EndConnect(result);
                    
                    Console.WriteLine("Connected to producer!");
                    
                    using (NetworkStream stream = client.GetStream())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string line;
                        
                        // Process incoming data
                        while ((line = reader.ReadLine()) != null)
                        {
                            // Check for termination signal
                            if (line == EndMarker)
                            {
                                Console.WriteLine("Received termination signal");
                                break;
                            }

                            // Process the data
                            if (int.TryParse(line, out int value))
                            {
                                int processedValue = value * value;
                                Console.WriteLine($"Consumed: {value}, Processed result: {processedValue}");
                                
                                // Simulate processing time
                                Thread.Sleep(1500); // 1.5 seconds
                            }
                            else
                            {
                                Console.WriteLine($"Received invalid data: {line}");
                            }
                        }
                    }
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
    }
}
