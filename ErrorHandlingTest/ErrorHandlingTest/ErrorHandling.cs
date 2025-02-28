using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IPCErrorHandlingTest
{
    class Program2
    {
        private const int Port = 8080;
        private const string EndMarker = "DONE";
        private const int NumItems = 20;
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please specify 'producer', 'consumer', or 'crasher' as a command line argument.");
                return;
            }

            string mode = args[0].ToLower();

            switch (mode)
            {
                case "producer":
                    RunProducer();
                    break;
                    
                case "consumer":
                    RunConsumer();
                    break;
                    
                case "crasher":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Please specify a crash time in seconds as the second argument.");
                        return;
                    }
                    
                    if (!int.TryParse(args[1], out int crashTime))
                    {
                        Console.WriteLine("Invalid crash time. Please specify a number of seconds.");
                        return;
                    }
                    
                    RunCrasher(crashTime);
                    break;
                    
                default:
                    Console.WriteLine("Invalid mode. Please specify 'producer', 'consumer', or 'crasher'.");
                    break;
            }
        }

        static void RunProducer()
        {
            Console.WriteLine("=== Producer Starting - Error Handling Test ===");
            Console.WriteLine($"Will attempt to send {NumItems} items.");

            // Create a TCP/IP socket with retry logic
            TcpListener listener = null;
            int retryCount = 0;
            const int maxRetries = 5;
            
            // Set up listeners for Ctrl+C and app domain unload
            Console.CancelKeyPress += (sender, e) => 
            {
                Console.WriteLine("\nCtrl+C detected. Shutting down gracefully...");
                e.Cancel = true;  // Prevent the process from terminating immediately
                cancellationTokenSource.Cancel();
            };
            
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => 
            {
                Console.WriteLine("\nApplication shutting down. Cleaning up...");
                cancellationTokenSource.Cancel();
                Thread.Sleep(500); // Give other threads time to clean up
            };
            
            // Setup connection with retry logic
            while (retryCount < maxRetries)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Loopback, Port);
                    listener.Start();
                    Console.WriteLine($"Producer listening on port {Port}");
                    break;
                }
                catch (SocketException ex)
                {
                    retryCount++;
                    Console.WriteLine($"Socket error on startup: {ex.Message}");
                    Console.WriteLine($"Retry {retryCount}/{maxRetries} in 2 seconds...");
                    Thread.Sleep(2000);
                    
                    if (retryCount >= maxRetries)
                    {
                        Console.WriteLine("Maximum retries reached. Unable to start producer.");
                        return;
                    }
                }
            }
            
            int itemsSent = 0;
            int reconnectCount = 0;
            const int maxReconnects = 3;
            
            while (itemsSent < NumItems && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                TcpClient client = null;
                NetworkStream stream = null;
                StreamWriter writer = null;
                
                try
                {
                    Console.WriteLine("Waiting for consumer to connect...");
                    client = listener.AcceptTcpClient();
                    Console.WriteLine("Consumer connected!");
                    
                    stream = client.GetStream();
                    writer = new StreamWriter(stream) { AutoFlush = true };
                    
                    // Send current status
                    writer.WriteLine($"STATUS|{itemsSent}|{NumItems}");
                    
                    // Resume sending items
                    int startItem = itemsSent;
                    for (int i = startItem + 1; i <= NumItems; i++)
                    {
                        // Check for cancellation
                        if (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Console.WriteLine("Cancellation requested. Stopping item production.");
                            break;
                        }
                        
                        // Generate test data
                        int data = i * 10;
                        
                        // Send the data with a sequence number
                        string message = $"DATA|{i}|{data}";
                        writer.WriteLine(message);
                        
                        Console.WriteLine($"Produced: {data} (item {i}/{NumItems})");
                        itemsSent = i;
                        
                        // Simulate production time
                        Thread.Sleep(1000);
                    }
                    
                    // Send termination signal if all items sent
                    if (itemsSent >= NumItems)
                    {
                        writer.WriteLine(EndMarker);
                        Console.WriteLine($"Sent termination signal: {EndMarker}");
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"\n[ERROR] I/O Exception: {ex.Message}");
                    Console.WriteLine($"Items sent before error: {itemsSent}/{NumItems}");
                    
                    reconnectCount++;
                    if (reconnectCount > maxReconnects)
                    {
                        Console.WriteLine("Maximum reconnection attempts reached. Giving up.");
                        break;
                    }
                    
                    Console.WriteLine($"Will accept new connections. Reconnect attempt {reconnectCount}/{maxReconnects}");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"\n[ERROR] Socket Exception: {ex.Message}");
                    Console.WriteLine($"Items sent before error: {itemsSent}/{NumItems}");
                    
                    reconnectCount++;
                    if (reconnectCount > maxReconnects)
                    {
                        Console.WriteLine("Maximum reconnection attempts reached. Giving up.");
                        break;
                    }
                    
                    Console.WriteLine($"Will accept new connections. Reconnect attempt {reconnectCount}/{maxReconnects}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[ERROR] Unexpected error: {ex.GetType().Name} - {ex.Message}");
                    Console.WriteLine($"Items sent before error: {itemsSent}/{NumItems}");
                    break;
                }
                finally
                {
                    // Clean up resources
                    writer?.Dispose();
                    stream?.Dispose();
                    client?.Dispose();
                }
            }
            
            // Final cleanup
            try
            {
                listener?.Stop();
            }
            catch { /* Ignore errors during cleanup */ }
            
            // Report final status
            if (itemsSent >= NumItems)
            {
                Console.WriteLine($"\n=== Producer Completed Successfully ===");
                Console.WriteLine($"All {NumItems} items were sent successfully.");
            }
            else
            {
                Console.WriteLine($"\n=== Producer Terminated Early ===");
                Console.WriteLine($"Sent {itemsSent}/{NumItems} items before termination.");
            }
            
            if (reconnectCount > 0)
            {
                Console.WriteLine($"Required {reconnectCount} reconnection(s) to complete.");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }

        static void RunConsumer()
        {
            Console.WriteLine("=== Consumer Starting - Error Handling Test ===");
            
            // Set up listeners for Ctrl+C and app domain unload
            Console.CancelKeyPress += (sender, e) => 
            {
                Console.WriteLine("\nCtrl+C detected. Shutting down gracefully...");
                e.Cancel = true;  // Prevent the process from terminating immediately
                cancellationTokenSource.Cancel();
            };
            
            int receivedItems = 0;
            int expectedItems = NumItems; // Default, will be updated when producer sends status
            int connectionAttempts = 0;
            const int maxConnectionAttempts = 10;
            bool receivedTermination = false;
            
            // Continue reconnecting until all items received or max retries
            while (!receivedTermination && connectionAttempts < maxConnectionAttempts && 
                   !cancellationTokenSource.Token.IsCancellationRequested)
            {
                connectionAttempts++;
                Console.WriteLine($"Connection attempt {connectionAttempts}/{maxConnectionAttempts}");
                Console.WriteLine("Connecting to producer...");
                
                TcpClient client = null;
                NetworkStream stream = null;
                StreamReader reader = null;
                
                try
                {
                    // Create a TCP/IP client with timeout
                    client = new TcpClient();
                    
                    // Connect with timeout
                    IAsyncResult connectResult = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                    bool success = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    
                    if (!success)
                    {
                        throw new TimeoutException("Timed out connecting to producer");
                    }
                    
                    // Complete the connection
                    client.EndConnect(connectResult);
                    Console.WriteLine("Connected to producer!");
                    
                    stream = client.GetStream();
                    reader = new StreamReader(stream);
                    
                    string line;
                    
                    // Process incoming data
                    while ((line = reader.ReadLine()) != null && !cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        // Check for termination signal
                        if (line == EndMarker)
                        {
                            Console.WriteLine("Received termination signal.");
                            receivedTermination = true;
                            break;
                        }
                        
                        // Parse the message
                        string[] parts = line.Split('|');
                        string messageType = parts[0];
                        
                        if (messageType == "STATUS")
                        {
                            // STATUS|itemsSent|totalItems
                            int.TryParse(parts[1], out int itemsAlreadySent);
                            int.TryParse(parts[2], out expectedItems);
                            
                            Console.WriteLine($"Status update from producer: {itemsAlreadySent}/{expectedItems} items sent so far.");
                            
                            if (itemsAlreadySent > receivedItems)
                            {
                                Console.WriteLine($"Warning: {itemsAlreadySent - receivedItems} items were lost due to previous disconnection.");
                                receivedItems = itemsAlreadySent;
                            }
                        }
                        else if (messageType == "DATA")
                        {
                            // DATA|sequenceNumber|value
                            int.TryParse(parts[1], out int sequenceNumber);
                            int.TryParse(parts[2], out int value);
                            
                            // Process the data
                            int processedValue = value * value;
                            receivedItems = sequenceNumber;
                            
                            Console.WriteLine($"Consumed: {value}, Processed result: {processedValue} (item {sequenceNumber}/{expectedItems})");
                            
                            // Simulate processing time
                            Thread.Sleep(1500); // 1.5 seconds
                        }
                        else
                        {
                            Console.WriteLine($"Received unknown message type: {messageType}");
                        }
                    }
                    
                    // If we reach here normally, connection closed by producer
                    if (!receivedTermination && !cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("Connection closed by producer without termination signal.");
                    }
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Timeout connecting to producer. Will retry in 5 seconds...");
                    Thread.Sleep(5000);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"\n[ERROR] Socket Exception: {ex.Message}");
                    Console.WriteLine("Will retry connecting in 5 seconds...");
                    Thread.Sleep(5000);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"\n[ERROR] I/O Exception: {ex.Message}");
                    Console.WriteLine($"Connection lost. Received {receivedItems}/{expectedItems} items before error.");
                    Console.WriteLine("Will attempt to reconnect in 5 seconds...");
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[ERROR] Unexpected error: {ex.GetType().Name} - {ex.Message}");
                    Console.WriteLine("Will retry connecting in 5 seconds...");
                    Thread.Sleep(5000);
                }
                finally
                {
                    // Clean up resources
                    reader?.Dispose();
                    stream?.Dispose();
                    client?.Dispose();
                }
            }
            
            // Report final status
            Console.WriteLine("\n=== Consumer Status ===");
            if (receivedTermination)
            {
                Console.WriteLine($"Successfully received all {receivedItems} items and termination signal.");
                Console.WriteLine("=== Consumer Completed Successfully ===");
            }
            else if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                Console.WriteLine($"Consumer cancelled. Received {receivedItems}/{expectedItems} items before cancellation.");
                Console.WriteLine("=== Consumer Terminated by User ===");
            }
            else if (connectionAttempts >= maxConnectionAttempts)
            {
                Console.WriteLine($"Maximum connection attempts reached ({maxConnectionAttempts}).");
                Console.WriteLine($"Received {receivedItems}/{expectedItems} items before giving up.");
                Console.WriteLine("=== Consumer Failed to Complete ===");
            }
            
            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
        
        static void RunCrasher(int crashAfterSeconds)
        {
            Console.WriteLine($"=== Crasher Starting - Will crash after {crashAfterSeconds} seconds ===");
            Console.WriteLine("Connecting to producer...");
            
            TcpClient client = null;
            NetworkStream stream = null;
            
            try
            {
                // Create a TCP/IP client with timeout
                client = new TcpClient();
                
                // Connect with timeout
                IAsyncResult connectResult = client.BeginConnect(IPAddress.Loopback, Port, null, null);
                bool success = connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                
                if (!success)
                {
                    Console.WriteLine("Timed out connecting to producer.");
                    return;
                }
                
                // Complete the connection
                client.EndConnect(connectResult);
                Console.WriteLine("Connected to producer!");
                
                stream = client.GetStream();
                StreamReader reader = new StreamReader(stream);
                
                Console.WriteLine($"Will simulate a crash in {crashAfterSeconds} seconds...");
                
                // Start a timer to simulate crash
                Task.Run(async () => 
                {
                    await Task.Delay(crashAfterSeconds * 1000);
                    Console.WriteLine("\n=== SIMULATING CRASH NOW ===");
                    // Force socket to close abruptly
                    client.Close();
                    Console.WriteLine("Socket forcibly closed. Press Enter to exit.");
                });
                
                // Read until crash happens
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Console.WriteLine($"Received: {line}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
            
            Console.ReadLine();
        }
    }
}
