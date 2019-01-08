using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Dataflow
{
    class Program
    {
        static void Main(string[] args)
        {

            CustomDataflowBlock.CoustomMain();


         //
         // Create the members of the pipeline.
         // 

         // Downloads the requested resource as a string. 

         //TransformBlock 使得管道的每个成员对其输入数据执行操作，并将结果发送到管道中的下一步
         var downloadString = new TransformBlock<string, string>(async uri =>
            {
                Console.WriteLine("Downloading '{0}'...", uri);

                return await new HttpClient().GetStringAsync(uri);
            });

            // Separates the specified text into an array of words.
            var createWordList = new TransformBlock<string, string[]>(text =>
            {
                Console.WriteLine("Creating word list...");

                // Remove common punctuation by replacing all non-letter characters 
                // with a space character.
                char[] tokens = text.Select(c => char.IsLetter(c) ? c : ' ').ToArray();
                text = new string(tokens);

                // Separate the text into an array of words.
                return text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            });

            // Removes short words and duplicates.
            var filterWordList = new TransformBlock<string[], string[]>(words =>
            {
                Console.WriteLine("Filtering word list...");

                return words
                   .Where(word => word.Length > 3)
                   .Distinct()
                   .ToArray();
            });

            // Finds all words in the specified collection whose reverse also 
            // exists in the collection.  

            // TransformManyBlock为每个输入产生多个独立输出
            var findReversedWords = new TransformManyBlock<string[], string>(words =>
            {
                Console.WriteLine("Finding reversed words...");

                var wordsSet = new HashSet<string>(words);

                return from word in words.AsParallel()
                       let reverse = new string(word.Reverse().ToArray())
                       where word != reverse && wordsSet.Contains(reverse)
                       select word;
            });

            // Prints the provided reversed words to the console. 

            //ActionBlock 它对其输入执行操作，并且不会产生结果  
            var printReversedWords = new ActionBlock<string>(reversedWord =>
            {
                Console.WriteLine("Found reversed words {0}/{1}",
                   reversedWord, new string(reversedWord.Reverse().ToArray()));
            });


            //添加以下代码以将每个块连接到管道中的下一个块。
            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };    //管道中一个块的成功或不成功完成将导致管道中下一个块的完成

            downloadString.LinkTo(createWordList, linkOptions);
            createWordList.LinkTo(filterWordList, linkOptions);
            filterWordList.LinkTo(findReversedWords, linkOptions);
            findReversedWords.LinkTo(printReversedWords, linkOptions);

            //将URL发布到数据流管道的头部。

            //您可以通过将输入数据发送到头节点（目标块），以及从管道的终端节点或网络的终端节点（一个或多个源块）接收输出数据，将这些方法与数据流管道或网络组合。
            // Process "The Iliad of Homer" by Homer.
            downloadString.Post("http://www.gutenberg.org/files/6130/6130-0.txt");    //  将输入数据发送到目标块


            //添加以下代码以将管道的头部标记为已完成。管道的头部在处理所有缓冲的消息之后传播其完成。
            //如果通过管道发送多个输入，请在提交所有输入后调用IDataflowBlock.Complete方法。
            //如果您的应用程序没有明确数据不再可用或者应用程序不必等待管道完成，则可以省略此步骤。
            // Mark the head of the pipeline as complete.
            downloadString.Complete();

            // Wait for the last block in the pipeline to process all messages.
            printReversedWords.Completion.Wait();

        }
    }




    class DataflowProducerConsumer
    {
        // Demonstrates the production end of the producer and consumer pattern.
        static void Produce(ITargetBlock<byte[]> target)
        {
            // Create a Random object to generate random data.
            Random rand = new Random();

            // In a loop, fill a buffer with random data and
            // post the buffer to the target block.
            for (int i = 0; i < 100; i++)
            {
                // Create an array to hold random byte data.
                byte[] buffer = new byte[1024];

                // Fill the buffer with random bytes.
                rand.NextBytes(buffer);

                // Post the result to the message block.
                target.Post(buffer);
            }

            // Set the target to the completed state to signal to the consumer
            // that no more data will be available.
            target.Complete();
        }

        // Demonstrates the consumption end of the producer and consumer pattern.
        static async Task<int> ConsumeAsync(ISourceBlock<byte[]> source)
        {
            // Initialize a counter to track the number of bytes that are processed.
            int bytesProcessed = 0;

            // Read from the source buffer until the source buffer has no 
            // available output data.
            while (await source.OutputAvailableAsync())
            {
                byte[] data = source.Receive();

                // Increment the count of bytes received.
                bytesProcessed += data.Length;
            }

            return bytesProcessed;
        }

        static async Task<int> ConsumeAsync(IReceivableSourceBlock<byte[]> source)
        {
            // Initialize a counter to track the number of bytes that are processed.
            int bytesProcessed = 0;

            // Read from the source buffer until the source buffer has no 
            // available output data.
            while (await source.OutputAvailableAsync())
            {
                byte[] data;
                while (source.TryReceive(out data)) //没有数据可用时，TryReceive方法返回False。当多个使用者必须同时访问源块时，此机制可确保在调用OutputAvailableAsync之后数据仍然可用。
                {
                    // Increment the count of bytes received.
                    bytesProcessed += data.Length;
                }
            }

            return bytesProcessed;
        }




        static void Main3(string[] args)
        {
            // Create a BufferBlock<byte[]> object. This object serves as the 
            // target block for the producer and the source block for the consumer.
            var buffer = new BufferBlock<byte[]>();

            // Start the consumer. The Consume method runs asynchronously. 
            var consumer = ConsumeAsync(buffer);

            // Post source data to the dataflow block.
            Produce(buffer);

            // Wait for the consumer to process all data.
            consumer.Wait();

            // Print the count of bytes processed to the console.
            Console.WriteLine("Processed {0} bytes.", consumer.Result);
        }
    }




    // Demonstrates how to provide delegates to exectution dataflow blocks.
    class DataflowExecutionBlocks
    {
        // Computes the number of zero bytes that the provided file
        // contains.
        static int CountBytes(string path)
        {
            byte[] buffer = new byte[1024];
            int totalZeroBytesRead = 0;
            using (var fileStream = File.OpenRead(path))
            {
                int bytesRead = 0;
                do
                {
                    bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    totalZeroBytesRead += buffer.Count(b => b == 0);
                } while (bytesRead > 0);
            }

            return totalZeroBytesRead;
        }

        static void Main2(string[] args)
        {
            // Create a temporary file on disk.
            string tempFile = Path.GetTempFileName();

            // Write random data to the temporary file.
            using (var fileStream = File.OpenWrite(tempFile))
            {
                Random rand = new Random();
                byte[] buffer = new byte[1024];
                for (int i = 0; i < 512; i++)
                {
                    rand.NextBytes(buffer);
                    fileStream.Write(buffer, 0, buffer.Length);
                }
            }

            // Create an ActionBlock<int> object that prints to the console 
            // the number of bytes read.
            var printResult = new ActionBlock<int>(zeroBytesRead =>
            {
                Console.WriteLine("{0} contains {1} zero bytes.",
                   Path.GetFileName(tempFile), zeroBytesRead);
            });

            // Create a TransformBlock<string, int> object that calls the 
            // CountBytes function and returns its result.
            var countBytes = new TransformBlock<string, int>(
               new Func<string, int>(CountBytes));

            // Link the TransformBlock<string, int> object to the 
            // ActionBlock<int> object.
            countBytes.LinkTo(printResult);

            // Create a continuation task that completes the ActionBlock<int>
            // object when the TransformBlock<string, int> finishes.
            countBytes.Completion.ContinueWith(delegate { printResult.Complete(); });

            // Post the path to the temporary file to the 
            // TransformBlock<string, int> object.
            countBytes.Post(tempFile);

            // Requests completion of the TransformBlock<string, int> object.
            countBytes.Complete();

            // Wait for the ActionBlock<int> object to print the message.
            printResult.Completion.Wait();

            // Delete the temporary file.
            File.Delete(tempFile);
        }
    }




    // Demonstrates how to unlink dataflow blocks.
    class DataflowReceiveAny
    {
        // Receives the value from the first provided source that has 
        // a message.
        public static T ReceiveFromAny<T>(params ISourceBlock<T>[] sources)
        {
            // Create a WriteOnceBlock<T> object and link it to each source block.
            var writeOnceBlock = new WriteOnceBlock<T>(e => e);
            foreach (var source in sources)
            {
                // Setting MaxMessages to one instructs
                // the source block to unlink from the WriteOnceBlock<T> object
                // after offering the WriteOnceBlock<T> object one message.
                source.LinkTo(writeOnceBlock, new DataflowLinkOptions { MaxMessages = 1 });
            }
            // Return the first value that is offered to the WriteOnceBlock object.
            return writeOnceBlock.Receive();
        }

        // Demonstrates a function that takes several seconds to produce a result.
        static int TrySolution(int n, CancellationToken ct)
        {
            // Simulate a lengthy operation that completes within three seconds
            // or when the provided CancellationToken object is cancelled.
            SpinWait.SpinUntil(() => ct.IsCancellationRequested,
               new Random().Next(3000));

            // Return a value.
            return n + 42;
        }

        static void Main1(string[] args)
        {
            // Create a shared CancellationTokenSource object to enable the 
            // TrySolution method to be cancelled.
            var cts = new CancellationTokenSource();

            // Create three TransformBlock<int, int> objects. 
            // Each TransformBlock<int, int> object calls the TrySolution method.
            Func<int, int> action = n => TrySolution(n, cts.Token);
            var trySolution1 = new TransformBlock<int, int>(action);
            var trySolution2 = new TransformBlock<int, int>(action);
            var trySolution3 = new TransformBlock<int, int>(action);

            // Post data to each TransformBlock<int, int> object.
            trySolution1.Post(11);
            trySolution2.Post(21);
            trySolution3.Post(31);

            // Call the ReceiveFromAny<T> method to receive the result from the 
            // first TransformBlock<int, int> object to finish.
            int result = ReceiveFromAny(trySolution1, trySolution2, trySolution3);

            // Cancel all calls to TrySolution that are still active.
            cts.Cancel();

            // Print the result to the console.
            Console.WriteLine("The solution is {0}.", result);

            cts.Dispose();
        }
    }



}
