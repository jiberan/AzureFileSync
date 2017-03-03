using GoMonkeys.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Files;
using Microsoft.WindowsAzure.MobileServices.Sync;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace GoMonkeys
{

    public class MonkeyDataManager
    {
        IDataService azureService;
        IMobileServiceSyncTable<Monkey> monkeyTable;
        IMobileServiceSyncTable<TodoItem> todoItem;

        IFileHelper fileHelper;
        private static TablogHandler _handler;


        public static TablogHandler Handler
        {
            get
            {
                if(_handler == null)
                   _handler  = new TablogHandler();
                return _handler;
            }
        }

        public MonkeyDataManager()
        {
            azureService = Xamarin.Forms.DependencyService.Get<IDataService>();
            monkeyTable = azureService.MonkeyTable;
            todoItem = azureService.TodoItem;
            fileHelper = Xamarin.Forms.DependencyService.Get<IFileHelper>();
        }

        public async Task SyncAsync()
        {
            ReadOnlyCollection<MobileServiceTableOperationError> syncErrors = null;

            try
            {

                await this.monkeyTable.MobileServiceClient.SyncContext.PushAsync();
                // FILES: Push file changes
               // await this.monkeyTable.PushFileChangesAsync();

                // FILES: Automatic pull
                // A normal pull will automatically process new/modified/deleted files, engaging the file sync handler
                Handler.ExpandPath = "TodoItems";
                await this.monkeyTable.PullAsync("allmonkeys",null);

            }
            catch (MobileServicePushFailedException exc)
            {
                if (exc.PushResult != null)
                {
                    syncErrors = exc.PushResult.Errors;
                }
            }

            // Simple error/conflict handling. A real application would handle the various errors like network conditions,
            // server conflicts and others via the IMobileServiceSyncHandler.
            if (syncErrors != null)
            {
                foreach (var error in syncErrors)
                {
                    if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Result != null)
                    {
                        //Update failed, reverting to server's copy.
                        await error.CancelAndUpdateItemAsync(error.Result);
                    }
                    else
                    {
                        // Discard local change.
                        await error.CancelAndDiscardItemAsync();
                    }
                }
            }
        }

        public async Task<List<Monkey>> GetMonkeysAsync()
        {
            try
            {
                var monkeys1 = await monkeyTable.ToListAsync();
                var todoItems = await todoItem.ToListAsync();

                var monkeys = await monkeyTable.ReadAsync();


                return monkeys.ToList();
            }
            catch (MobileServiceInvalidOperationException msioe)
            {
                Debug.WriteLine(@"INVALID {0}", msioe.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"ERROR {0}", e.Message);
            }
            return null;
        }

        public async Task SaveMonkeyAsync(Monkey item)
        {
            if (item.Id == null)
            {
                await this.monkeyTable.InsertAsync(item);
            }
            else
                await this.monkeyTable.UpdateAsync(item);
        }

        public async Task SaveTodoItemAsync(TodoItem item)
        {
            if (item.Id == null)
            {
                await this.todoItem.InsertAsync(item);
            }
            else
                await this.todoItem.UpdateAsync(item);
        }

        public async Task DeleteMonkeyAsync(Monkey item)
        {
            try
            {
                await monkeyTable.DeleteAsync(item);
            }
            catch (MobileServiceInvalidOperationException msioe)
            {
                Debug.WriteLine(@"INVALID {0}", msioe.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"ERROR {0}", e.Message);
            }
        }

        internal async Task<MobileServiceFile> AddImage(Monkey monkey, string imagePath)
        {
            string targetPath = fileHelper.CopyFileToAppDirectory(monkey.Id, imagePath);

            // FILES: Creating/Adding file
            MobileServiceFile file = await this.monkeyTable.AddFileAsync(monkey, Path.GetFileName(targetPath));


            // "Touch" the record to mark it as updated
            await this.monkeyTable.UpdateAsync(monkey);

            return file;
        }

        internal async Task DeleteImage(Monkey monkey, MobileServiceFile file)
        {
            // FILES: Deleting file
            await this.monkeyTable.DeleteFileAsync(file);

            // "Touch" the record to mark it as updated
            await this.monkeyTable.UpdateAsync(monkey);
        }

        internal async Task<IEnumerable<MobileServiceFile>> GetImageFiles(Monkey monkey)
        {
            // FILES: Get files (local)
            //if (requiresServerPull)
            //    await this.monkeyTable.PullFilesAsync(todoItem);
            return await this.monkeyTable.GetFilesAsync(monkey);
        }
    }
    public interface IExpandingHandler
    {
        string ExpandPath { get; set; }

        void ExpandRequest(HttpRequestMessage request, string expandPath);
    }

    public class ExpandingHandler : DelegatingHandler, IExpandingHandler
    {
        public string ExpandPath { get; set; }

        protected bool ShouldExpandRequest
        {
            get { return !string.IsNullOrWhiteSpace(ExpandPath); }
            set { ExpandPath = null; }
        }

        public void ExpandRequest(HttpRequestMessage request, string expandPath)
        {
            UriBuilder builder = new UriBuilder(request.RequestUri);
            string query = builder.Query;
            if (!query.Contains("$expand"))
            {
                if (string.IsNullOrEmpty(query))
                {
                    query = string.Empty;
                }
                else
                {
                    query = query + "&";
                }

                query = query + "$expand=" + expandPath;
                builder.Query = query.TrimStart('?');
                request.RequestUri = builder.Uri;
            }
        }
    }

    public class TablogHandler : ExpandingHandler
    {
        private const string INDENT_STRING = "    ";

        private readonly bool _logRequestResponseBody;

     


        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {

            if (ShouldExpandRequest)
            {
                ExpandRequest(request, ExpandPath);
                ShouldExpandRequest = false;
            }

            if (Debugger.IsAttached)
            {
                Debug.WriteLine("Request:({2}) {0} {1}", request.Method, request.RequestUri, DateTime.Now.ToString("hh.mm.ss.ffffff"));

                if ( request.Content != null)
                {
                    var requestContent = await request.Content.ReadAsStringAsync();
                    Debug.WriteLine(FormatJson(requestContent));
                }
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (Debugger.IsAttached)
            {
                Debug.WriteLine("Response:({1}) {0}", response.StatusCode, DateTime.Now.ToString("hh.mm.ss.ffffff"));

                //if (_logRequestResponseBody)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(FormatJson(responseContent));
                }
            }

            return response;
        }

        private string FormatJson(string json)
        {
            int indentation = 0;
            int quoteCount = 0;
            var result =
                from ch in json.ToCharArray()
                let quotes = ch == '"' ? quoteCount++ : quoteCount
                let lineBreak = ch == ',' && quotes % 2 == 0 ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(INDENT_STRING, indentation)) : null
                let openChar = ch == '{' || ch == '[' ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(INDENT_STRING, ++indentation)) : ch.ToString()
                let closeChar = ch == '}' || ch == ']' ? Environment.NewLine + string.Concat(Enumerable.Repeat(INDENT_STRING, --indentation)) + ch : ch.ToString()
                select lineBreak == null
                            ? openChar.Length > 1
                                ? openChar
                                : closeChar
                            : lineBreak;

            return string.Concat(result);
        }
    }
}
