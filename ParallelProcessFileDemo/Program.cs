using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelProcessFileDemo
{
    class Program
    {
        static void Main()
        {

            // ProcessFiles();

            // TaskPararmeterErrorInLoop();
            //TaskParameterRightInLoop();
            DownloadChars();

        }



        private static void DownloadChars()
        {
            // The URLs to download.
            string[] urls = new string[]
            {
                "http://msdn.microsoft.com",
                "http://www.contoso.com",
                "http://www.microsoft.com"
            };

            // Used to time download operations.
            Stopwatch stopwatch = new Stopwatch();

            // Compute the time required to download the URLs.
            stopwatch.Start();
            var downloads = from url in urls
                            select DownloadStringAsync(url);
            Task.WhenAll(downloads).ContinueWith(results =>
                {
                    stopwatch.Stop();

                    // Print the number of characters download and the elapsed time.
                    Console.WriteLine("Retrieved {0} characters. Elapsed time was {1} ms.",
                        results.Result.Sum(result => result.Length),
                        stopwatch.ElapsedMilliseconds);
                })
                .Wait();

            // Perform the same operation a second time. The time required
            // should be shorter because the results are held in the cache.
            stopwatch.Restart();
            downloads = from url in urls
                        select DownloadStringAsync(url);
            Task.WhenAll(downloads).ContinueWith(results =>
                {
                    stopwatch.Stop();

                    // Print the number of characters download and the elapsed time.
                    Console.WriteLine("Retrieved {0} characters. Elapsed time was {1} ms.",
                        results.Result.Sum(result => result.Length),
                        stopwatch.ElapsedMilliseconds);
                })
                .Wait();
        }

        // Holds the results of download operations.
        static ConcurrentDictionary<string, string> cachedDownloads =
            new ConcurrentDictionary<string, string>();

        // Asynchronously downloads the requested resource as a string.
        public static Task<string> DownloadStringAsync(string address)
        {
            // First try to retrieve the content from cache.
            string content;
            if (cachedDownloads.TryGetValue(address, out content))
            {
                return Task.FromResult<string>(content);
            }

            // If the result was not in the cache, download the 
            // string and add it to the cache.
            return Task.Run(async () =>
            {
                content = await new WebClient().DownloadStringTaskAsync(address);
                cachedDownloads.TryAdd(address, content);
                return content;
            });
        }






        #region TAP
        class CustomData
        {
            public long CreationTime;
            public int Name;
            public int ThreadNum;
        }
        private static void TaskPararmeterErrorInLoop()
        {
            // Create the task object by using an Action(Of Object) to pass in the loop
            // counter. This produces an unexpected result.
            Task[] taskArray = new Task[10];
            for (int i = 0; i < taskArray.Length; i++)
            {
                taskArray[i] = Task.Factory.StartNew((Object obj) =>
                {
                    //var data = new CustomData() { Name = i , CreationTime = DateTime.Now.Ticks };
                    var data = new CustomData() { Name = Convert.ToInt32(obj), CreationTime = DateTime.Now.Ticks };
                    data.ThreadNum = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine("Task #{0} created at {1} on thread #{2}.",
                        data.Name, data.CreationTime, data.ThreadNum);
                }, i);
            }
            Task.WaitAll(taskArray);
        }

        private static void TaskParameterRightInLoop()
        {
            // Create the task object by using an Action(Of Object) to pass in custom data
            // to the Task constructor. This is useful when you need to capture outer variables
            // from within a loop. 
            Task[] taskArray = new Task[10];
            for (int i = 0; i < taskArray.Length; i++)
            {
                taskArray[i] = Task.Factory.StartNew((Object obj) =>
                {
                    CustomData data = obj as CustomData;
                    if (data == null)
                        return;

                    data.ThreadNum = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine("Task #{0} created at {1} on thread #{2}.",
                        data.Name, data.CreationTime, data.ThreadNum);
                }, new CustomData() { Name = i, CreationTime = DateTime.Now.Ticks });
            }

            Task.WaitAll(taskArray);
        }

        #endregion






        #region Paralle Process File
        private static void ProcessFiles()
        {
            try
            {
                TraverseTreeParallelForEach(@"C:\Program Files", (f) =>
                {
                    // Exceptions are no-ops.
                    try
                    {
                        // Do nothing with the data except read it.
                        byte[] data = File.ReadAllBytes(f);
                    }
                    catch (FileNotFoundException) { }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (SecurityException) { }
                    // Display the filename.
                    Console.WriteLine(f);
                });
            }
            catch (ArgumentException)
            {
                Console.WriteLine(@"The directory 'C:\Program Files' does not exist.");
            }

            // Keep the console window open.
            Console.ReadKey();
        }


        public static void TraverseTreeParallelForEach(string root, Action<string> action)
        {
            //Count of files traversed and timer for diagnostic output
            int fileCount = 0;
            var sw = Stopwatch.StartNew();

            // Determine whether to parallelize file processing on each folder based on processor count.
            int procCount = System.Environment.ProcessorCount;

            // Data structure to hold names of subfolders to be examined for files.
            Stack<string> dirs = new Stack<string>();

            if (!Directory.Exists(root))
            {
                throw new ArgumentException();
            }
            dirs.Push(root);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs = { };
                string[] files = { };

                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                }
                // Thrown if we do not have discovery permission on the directory.
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                // Thrown if another process has deleted the directory after we retrieved its name.
                catch (DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                try
                {
                    files = Directory.GetFiles(currentDir);
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (IOException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                // Execute in parallel if there are enough files in the directory.
                // Otherwise, execute sequentially.Files are opened and processed
                // synchronously but this could be modified to perform async I/O.
                try
                {
                    if (files.Length < procCount)
                    {
                        foreach (var file in files)
                        {
                            action(file);
                            fileCount++;
                        }
                    }
                    else
                    {
                        Parallel.ForEach(files, () => 0, (file, loopState, localCount) =>
                        {
                            action(file);
                            return (int)++localCount;
                        },
                                         (c) =>
                                         {
                                             Interlocked.Add(ref fileCount, c);
                                         });
                    }
                }
                catch (AggregateException ae)
                {
                    ae.Handle((ex) =>
                    {
                        if (ex is UnauthorizedAccessException)
                        {
                            // Here we just output a message and go on.
                            Console.WriteLine(ex.Message);
                            return true;
                        }
                        // Handle other exceptions here if necessary...

                        return false;
                    });
                }

                // Push the subdirectories onto the stack for traversal.
                // This could also be done before handing the files.
                foreach (string str in subDirs)
                    dirs.Push(str);
            }

            // For diagnostic purposes.
            Console.WriteLine("Processed {0} files in {1} milliseconds", fileCount, sw.ElapsedMilliseconds);
        }


        #endregion Paralle Process File





        #region Task Parallel Library (TPL)

        #region FromAsync Implement    [Asynchronous Programming Model (APM)]    Wrapping APM Operations in a Task
        /// <summary>
        /// 异步IO调用实例
        /// </summary>
        private static void ProcessFileAsync()
        {

            var path = "";
            Task<string> t = GetFileStringAsync(path);

            // Do some other work:
            // ...

            try
            {
                Console.WriteLine(t.Result.Substring(0, 500));
            }
            catch (AggregateException ae)
            {
                Console.WriteLine(ae.InnerException.Message);
            }
        }


        const int MAX_FILE_SIZE = 14000000;
        public static Task<string> GetFileStringAsync(string path)
        {
            FileInfo fi = new FileInfo(path);
            byte[] data = null;
            data = new byte[fi.Length];

            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, data.Length, true);

            //Task<int> returns the number of bytes read
            Task<int> task = Task<int>.Factory.FromAsync(
                fs.BeginRead, fs.EndRead, data, 0, data.Length, null);

            // It is possible to do other work here while waiting
            // for the antecedent task to complete.
            // ...

            // Add the continuation, which returns a Task<string>. 
            return task.ContinueWith((antecedent) =>
            {
                fs.Close();

                // Result = "number of bytes read" (if we need it.)
                if (antecedent.Result < 100)
                {
                    return "Data is too small to bother with.";
                }
                else
                {
                    // If we did not receive the entire file, the end of the
                    // data buffer will contain garbage.
                    if (antecedent.Result < data.Length)
                        Array.Resize(ref data, antecedent.Result);

                    // Will be returned in the Result property of the Task<string>
                    // at some future point after the asynchronous file I/O operation completes.
                    return new UTF8Encoding().GetString(data);
                }
            });
        }


        public Task<string> GetFileStringAsync2(string path)
        {
            FileInfo fi = new FileInfo(path);
            byte[] data = new byte[fi.Length];
            MyCustomState state = GetCustomState();
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, data.Length, true);
            // We still pass null for the last parameter because
            // the state variable is visible to the continuation delegate.
            Task<int> task = Task<int>.Factory.FromAsync(
                fs.BeginRead, fs.EndRead, data, 0, data.Length, null);

            return task.ContinueWith((antecedent) =>
            {
                // It is safe to close the filestream now.
                fs.Close();

                // Capture custom state data directly in the user delegate.
                // No need to pass it through the FromAsync method.
                if (state.StateData.Contains("New York, New York"))
                {
                    return "Start spreading the news!";
                }
                else
                {
                    // If we did not receive the entire file, the end of the
                    // data buffer will contain garbage.
                    if (antecedent.Result < data.Length)
                        Array.Resize(ref data, antecedent.Result);

                    // Will be returned in the Result property of the Task<string>
                    // at some future point after the asynchronous file I/O operation completes.
                    return new UTF8Encoding().GetString(data);
                }
            });

        }

        private MyCustomState GetCustomState()
        {
            throw new NotImplementedException();
        }


        public Task<string> GetMultiFileData(string[] filesToRead)
        {
            FileStream fs;
            Task<string>[] tasks = new Task<string>[filesToRead.Length];
            byte[] fileData = null;
            for (int i = 0; i < filesToRead.Length; i++)
            {
                fileData = new byte[0x1000];
                fs = new FileStream(filesToRead[i], FileMode.Open, FileAccess.Read, FileShare.Read, fileData.Length, true);

                // By adding the continuation here, the 
                // Result of each task will be a string.
                tasks[i] = Task<int>.Factory.FromAsync(
                        fs.BeginRead, fs.EndRead, fileData, 0, fileData.Length, null)
                    .ContinueWith((antecedent) =>
                    {
                        fs.Close();

                        // If we did not receive the entire file, the end of the
                        // data buffer will contain garbage.
                        if (antecedent.Result < fileData.Length)
                            Array.Resize(ref fileData, antecedent.Result);

                        // Will be returned in the Result property of the Task<string>
                        // at some future point after the asynchronous file I/O operation completes.
                        return new UTF8Encoding().GetString(fileData);
                    });
            }

            // Wait for all tasks to complete. 
            return Task<string>.Factory.ContinueWhenAll(tasks, (data) =>
            {
                // Propagate all exceptions and mark all faulted tasks as observed.
                Task.WaitAll(data);

                // Combine the results from all tasks.
                StringBuilder sb = new StringBuilder();
                foreach (var t in data)
                {
                    sb.Append(t.Result);
                }
                // Final result to be returned eventually on the calling thread.
                return sb.ToString();
            });

        }



        #endregion FromAsync Implement   [Asynchronous Programming Model (APM)]      Wrapping APM Operations in a Task




        #region TaskCompletionSource Implement    [Event-based asynchronous pattern (EAP)]    Exposing Complex EAP Operations As Tasks

        private void DownloadMain()
        {
            string[] addresses = { "http://www.msnbc.com", "http://www.yahoo.com",
                "http://www.nytimes.com", "http://www.washingtonpost.com",
                "http://www.latimes.com", "http://www.newsday.com" };
            CancellationTokenSource cts = new CancellationTokenSource();

            // Create a UI thread from which to cancel the entire operation
            Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Press c to cancel");
                if (Console.ReadKey().KeyChar == 'c')
                    cts.Cancel();
            });

            // Using a neutral search term that is sure to get some hits.
            Task<string[]> webTask = GetWordCounts(addresses, "the", cts.Token);

            // Do some other work here unless the method has already completed.
            if (!webTask.IsCompleted)
            {
                // Simulate some work.
                Thread.SpinWait(5000000);
            }

            string[] results = null;
            try
            {
                results = webTask.Result;
            }
            catch (AggregateException e)
            {
                foreach (var ex in e.InnerExceptions)
                {
                    OperationCanceledException oce = ex as OperationCanceledException;
                    if (oce != null)
                    {
                        if (oce.CancellationToken == cts.Token)
                        {
                            Console.WriteLine("Operation canceled by user.");
                        }
                    }
                    else
                        Console.WriteLine(ex.Message);
                }
            }
            finally
            {
                cts.Dispose();
            }
            if (results != null)
            {
                foreach (var item in results)
                    Console.WriteLine(item);
            }
            Console.ReadKey();
        }


        public Task<string[]> GetWordCounts(string[] urls, string name, CancellationToken token)
        {
            TaskCompletionSource<string[]> tcs = new TaskCompletionSource<string[]>();
            WebClient[] webClients = new WebClient[urls.Length];

            // If the user cancels the CancellationToken, then we can use the
            // WebClient's ability to cancel its own async operations.
            token.Register(() =>
            {
                foreach (var wc in webClients)
                {
                    if (wc != null)
                        wc.CancelAsync();
                }
            });

            object m_lock = new object();
            int count = 0;
            List<string> results = new List<string>();
            for (int i = 0; i < urls.Length; i++)
            {
                webClients[i] = new WebClient();

                #region callback
                // Specify the callback for the DownloadStringCompleted
                // event that will be raised by this WebClient instance.
                webClients[i].DownloadStringCompleted += (obj, args) =>
                {
                    if (args.Cancelled == true)
                    {
                        tcs.TrySetCanceled();
                        return;
                    }
                    else if (args.Error != null)
                    {
                        // Pass through to the underlying Task
                        // any exceptions thrown by the WebClient
                        // during the asynchronous operation.
                        tcs.TrySetException(args.Error);
                        return;
                    }
                    else
                    {
                        // Split the string into an array of words,
                        // then count the number of elements that match
                        // the search term.
                        string[] words = null;
                        words = args.Result.Split(' ');
                        string NAME = name.ToUpper();
                        int nameCount = (from word in words.AsParallel()
                                         where word.ToUpper().Contains(NAME)
                                         select word)
                                        .Count();

                        // Associate the results with the url, and add new string to the array that 
                        // the underlying Task object will return in its Result property.
                        results.Add(String.Format("{0} has {1} instances of {2}", args.UserState, nameCount, name));
                    }

                    // If this is the last async operation to complete,
                    // then set the Result property on the underlying Task.
                    lock (m_lock)
                    {
                        count++;
                        if (count == urls.Length)
                        {
                            tcs.TrySetResult(results.ToArray());
                        }
                    }
                };
                #endregion

                // Call DownloadStringAsync for each URL.
                Uri address = null;
                try
                {
                    address = new Uri(urls[i]);
                    // Pass the address, and also use it for the userToken 
                    // to identify the page when the delegate is invoked.
                    webClients[i].DownloadStringAsync(address, address);
                }

                catch (UriFormatException ex)
                {
                    // Abandon the entire operation if one url is malformed.
                    // Other actions are possible here.
                    tcs.TrySetException(ex);
                    return tcs.Task;
                }
            }

            // Return the underlying Task. The client code
            // waits on the Result property, and handles exceptions
            // in the try-catch block there.
            return tcs.Task;
        }

        public Task<string[]> GetWordCountsSimplified(string[] urls, string name,
                                          CancellationToken token)
        {
            TaskCompletionSource<string[]> tcs = new TaskCompletionSource<string[]>();
            WebClient[] webClients = new WebClient[urls.Length];
            object m_lock = new object();
            int count = 0;
            List<string> results = new List<string>();

            // If the user cancels the CancellationToken, then we can use the
            // WebClient's ability to cancel its own async operations.
            token.Register(() =>
            {
                foreach (var wc in webClients)
                {
                    if (wc != null)
                        wc.CancelAsync();
                }
            });


            for (int i = 0; i < urls.Length; i++)
            {
                webClients[i] = new WebClient();

                #region callback
                // Specify the callback for the DownloadStringCompleted
                // event that will be raised by this WebClient instance.
                webClients[i].DownloadStringCompleted += (obj, args) =>
                {

                    // Argument validation and exception handling omitted for brevity.

                    // Split the string into an array of words,
                    // then count the number of elements that match
                    // the search term.
                    string[] words = args.Result.Split(' ');
                    string NAME = name.ToUpper();
                    int nameCount = (from word in words.AsParallel()
                                     where word.ToUpper().Contains(NAME)
                                     select word)
                                    .Count();

                    // Associate the results with the url, and add new string to the array that
                    // the underlying Task object will return in its Result property.
                    lock (m_lock)
                    {
                        results.Add(String.Format("{0} has {1} instances of {2}", args.UserState, nameCount, name));

                        // If this is the last async operation to complete,
                        // then set the Result property on the underlying Task.
                        count++;
                        if (count == urls.Length)
                        {
                            tcs.TrySetResult(results.ToArray());
                        }
                    }
                };
                #endregion

                // Call DownloadStringAsync for each URL.
                Uri address = null;
                address = new Uri(urls[i]);
                webClients[i].DownloadStringAsync(address, address);

            } // end for

            // Return the underlying Task. The client code
            // waits on the Result property, and handles exceptions
            // in the try-catch block there.
            return tcs.Task;
        }

        #endregion TaskCompletionSource Implement   [Event-based asynchronous pattern (EAP)]    Exposing Complex EAP Operations As Tasks

        #endregion  Task Parallel Library (TPL)



        #region Task to APM
        private void Execute()
        {
            int decimalPlaces = 12;
            Calculator calc = new Calculator();
            int places = 35;

            AsyncCallback callBack = new AsyncCallback(PrintResult);
            IAsyncResult ar = calc.BeginCalculate(places, callBack, calc);

            // Do some work on this thread while the calulator is busy.
            Console.WriteLine("Working...");
            Thread.SpinWait(500000);
            Console.ReadLine();

        }
        public static void PrintResult(IAsyncResult result)
        {
            Calculator c = (Calculator)result.AsyncState;
            string piString = c.EndCalculate(result);
            Console.WriteLine("Calling PrintResult on thread {0}; result = {1}",
            Thread.CurrentThread.ManagedThreadId, piString);
        }


        class Calculator
        {
            public IAsyncResult BeginCalculate(int decimalPlaces, AsyncCallback ac, object state)
            {
                Console.WriteLine("Calling BeginCalculate on thread {0}", Thread.CurrentThread.ManagedThreadId);
                Task<string> f = Task<string>.Factory.StartNew(_ => Compute(decimalPlaces), state);
                if (ac != null) f.ContinueWith((res) => ac(f));
                return f;
            }

            public string Compute(int numPlaces)
            {
                Console.WriteLine("Calling compute on thread {0}", Thread.CurrentThread.ManagedThreadId);

                // Simulating some heavy work.
                Thread.SpinWait(500000000);

                // Actual implemenation left as exercise for the reader.
                // Several examples are available on the Web.
                return "3.14159265358979323846264338327950288";
            }

            public string EndCalculate(IAsyncResult ar)
            {
                Console.WriteLine("Calling EndCalculate on thread {0}", Thread.CurrentThread.ManagedThreadId);
                return ((Task<string>)ar).Result;
            }
        }

        #endregion     Task to APM



        /// <summary>
        /// Gets <see cref="FileData"/> for all the files in a directory that
        /// match a specific filter, optionally including all sub directories.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search string to match against files in the path.</param>
        /// <param name="searchOption">
        /// One of the SearchOption values that specifies whether the search
        /// operation should include all subdirectories or only the current directory.
        /// </param>
        /// <returns>An object that implements <see cref="IEnumerable{FileData}"/> and
        /// allows you to enumerate the files in the given directory.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="path"/> is a null reference (Nothing in VB)
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="filter"/> is a null reference (Nothing in VB)
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="searchOption"/> is not one of the valid values of the
        /// <see cref="System.IO.SearchOption"/> enumeration.
        /// </exception>
        /*    public static IEnumerable<FileData> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            {
                if (path == null)
                {
                    throw new ArgumentNullException("path");
                }
                if (searchPattern == null)
                {
                    throw new ArgumentNullException("searchPattern");
                }
                if ((searchOption != SearchOption.TopDirectoryOnly) && (searchOption != SearchOption.AllDirectories))
                {
                    throw new ArgumentOutOfRangeException("searchOption");
                }

                string fullPath = Path.GetFullPath(path);

                SafeFindHandle hndFindFile;
                WIN32_FIND_DATA win_find_data = new WIN32_FIND_DATA();
                Regex regExFilter = new Regex(WildcardToRegex(searchPattern), RegexOptions.Compiled | RegexOptions.IgnoreCase);

                Queue<String> qDirectories = new Queue<string>();
                qDirectories.Enqueue(Path.GetFullPath(path));

                while (qDirectories.Count > 0)
                {
                    String currentPath = qDirectories.Dequeue();

                    // We want to enumerate *all* directories so use a wildcard in our search.
                    // We may not want to enumerte *all* files, so we will filter them later.
                    String currentSearch = Path.Combine(currentPath, "*");
                    hndFindFile = FindFirstFile(currentSearch, win_find_data);

                    // Did we get some data?
                    if (!hndFindFile.IsInvalid)
                    {
                        // Yes. Parse the data and then call FindNextFIle.
                        do
                        {
                            // Is this a directory entry?
                            if (((FileAttributes)win_find_data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                            {
                                // Is it a sub-directory?
                                if ("." != win_find_data.cFileName &&
                                    ".." != win_find_data.cFileName)
                                {
                                    // Yes. Are we enumerating all directories?
                                    if (SearchOption.AllDirectories == searchOption)
                                    {
                                        // Yes. Queue it up!
                                        qDirectories.Enqueue(Path.Combine(currentPath, win_find_data.cFileName));
                                    }
                                }
                            }
                            else
                            {
                                // No. It is a file. Does it match our filter?
                                if (regExFilter.IsMatch(win_find_data.cFileName))
                                {
                                    yield return new FileData(currentPath, win_find_data);
                                }
                            }
                        }
                        while (FindNextFile(hndFindFile, win_find_data));

                        // We could check the return code from FindFirstFile / FindNextFile if we wanted to throw
                        // UnauthorizedAccessExceptions etc...
                        // int error = Marshal.GetLastWin32Error();
                    }
                }
            }        */

        public static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern).
                               Replace(@"\*", ".*").
                               Replace(@"\?", ".") + "$";
        }




    }


}
