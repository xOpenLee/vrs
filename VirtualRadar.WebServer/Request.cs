﻿// Copyright © 2010 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirtualRadar.Interface.WebServer;
using System.Net;
using System.IO;
using System.Collections.Specialized;
using System.Web;

namespace VirtualRadar.WebServer
{
    /// <summary>
    /// A wrapper around an HttpListenerRequest.
    /// </summary>
    class Request : IRequest
    {
        /// <summary>
        /// The underlying request object that we're wrapping.
        /// </summary>
        private HttpListenerRequest _Request;

        /// <summary>
        /// See interface docs.
        /// </summary>
        public CookieCollection Cookies
        {
            get { return _Request.Cookies; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public long ContentLength64
        {
            get { return _Request.ContentLength64; }
        }

        private NameValueCollection _FormValues;
        /// <summary>
        /// See interface docs.
        /// </summary>
        public NameValueCollection FormValues
        {
            get {
                if(_FormValues == null) {
                    _FormValues = new NameValueCollection();
                    ExtractFormValues();
                }
                return _FormValues;
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public int MaximumPostBodySize { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public NameValueCollection Headers
        {
            get { return _Request.Headers; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string HttpMethod
        {
            get { return _Request.HttpMethod; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public Stream InputStream
        {
            get { return _Request.InputStream; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool IsLocal
        {
            get { return _Request.IsLocal; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string RawUrl
        {
            get { return _Request.RawUrl; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return _Request.RemoteEndPoint; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _Request.LocalEndPoint; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public Uri Url
        {
            get { return _Request.Url; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string UserAgent
        {
            get { return _Request.UserAgent; }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string UserHostName
        {
            get { return _Request.UserHostName; }
        }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        /// <param name="request"></param>
        public Request(HttpListenerRequest request)
        {
            _Request = request;
            MaximumPostBodySize = 32 * 1024;
        }

        /// <summary>
        /// Extracts the form values from the body of an HTTP POST request.
        /// </summary>
        private void ExtractFormValues()
        {
            if(HttpMethod == "POST" && _Request.ContentType == "application/x-www-form-urlencoded") {
                if(_Request.ContentLength64 <= MaximumPostBodySize) {
                    string content = ReadBodyAsString(Encoding.UTF8);
                    if(content != null) {
                        var finished = false;
                        var start = 0;
                        while(!finished) {
                            var end = content.IndexOf('&');
                            if(end == -1) end = content.Length;
                            var nameValue = content.Substring(start, end - start);
                            var separator = nameValue.IndexOf('=');
                            var name = separator == -1 ? nameValue : nameValue.Substring(0, separator).Trim();
                            var value = separator == -1 ? "" : nameValue.Substring(separator + 1);
                            value = HttpUtility.UrlDecode(value);
                            if(!String.IsNullOrEmpty(name)) {
                                _FormValues[name] = value;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string ReadBodyAsString(Encoding encoding)
        {
            string result = null;

            using(var stream = InputStream) {
                using(var memory = new MemoryStream()) {
                    var bytesRead = 0;
                    var totalBytesRead = 0;
                    var buffer = new byte[128];
                    var failed = false;
                    do {
                        totalBytesRead += (bytesRead = stream.Read(buffer, 0, buffer.Length));
                        if(totalBytesRead > MaximumPostBodySize) failed = true;
                        else if(bytesRead != 0) memory.Write(buffer, 0, bytesRead);
                    } while(bytesRead != 0 && !failed);

                    if(!failed) {
                        var bytes = memory.ToArray();
                        result = encoding.GetString(bytes);
                    }
                }
            }

            return result;
        }
    }
}
