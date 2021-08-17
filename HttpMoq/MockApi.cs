﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HttpMoq
{
    public sealed class MockApi : IDisposable
    {
        private readonly IWebHost _host;
        private readonly List<Request> _requests = new();
        private Queue<string> _output = new();

        public MockApi(int port)
        {
            _host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls($"http://+:{port}")
                .Configure(app =>
                {
                    app.Use(async (context, _) =>
                    {
                        _output.Enqueue("Incoming request to: " + context.Request.GetDisplayUrl());

                        var request = Find(context.Request.Path.Value, context.Request.QueryString.Value, context.Request.Method);
                        if (request == null)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            context.Response.ContentType = "text/plain";

                            const string error = "No mock could be found to match this request.";

                            Print(error);

                            var bytes = Encoding.UTF8.GetBytes(error);
                            await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);

                            return;
                        }

                        request.Increment();
                        await request.Handle(context);
                    });
                })
                .Build();
        }

        public Request Get(string path)
        {
            var request = new Request(path, HttpMethods.Get);
            _requests.Add(request);

            return request;
        }

        public Request Post(string path)
        {
            var request = new Request(path, HttpMethods.Post);
            _requests.Add(request);
            
            return request;
        }

        public Request Put(string path)
        {
            var request = new Request(path, HttpMethods.Put);
            _requests.Add(request);

            return request;
        }

        public Request Patch(string path)
        {
            var request = new Request(path, HttpMethods.Patch);
            _requests.Add(request);

            return request;
        }

        public Request Delete(string path)
        {
            var request = new Request(path, HttpMethods.Delete);
            _requests.Add(request);

            return request;
        }

        public Request Expect(string method, string path)
        {
            var request = new Request(path, method);
            _requests.Add(request);

            return request;
        }

        internal Request Find(string path, string queryString, string method)
        {
            return _requests.FirstOrDefault(x => PathHelper.IsMatch(x.Path, path) && x.Method == method &&
                                                 (x.Query == null || QueryStringHelper.IsMatch(x.Query, queryString)));
        }

        public void Remove(Request request)
        {
            if (!_requests.Remove(request))
            {
                throw new InvalidOperationException("This request does not exist in this MockApi.");
            }
        }

        public Task StartAsync()
        {
            return _host.StartAsync();
        }

        public Task StopAsync()
        {
            return _host.StopAsync();
        }

        public void PrintOutput(Action<string> writer)
        {
            lock (_output)
            {
                var output = _output;
                _output = new Queue<string>();

                foreach (var o in output)
                {
                    writer.Invoke(o);
                }
            }
        }

        private void Print(string output)
        {
            lock (_output)
            {
                _output.Enqueue(output);
            }
        }

        public void Dispose()
        {
            _host?.Dispose();
        }
    }
}
