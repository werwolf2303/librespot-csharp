#region License
/*
 * WebSocketBehavior.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2025 sta.blockhead
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

using System;
using System.Collections.Specialized;
using System.IO;
using System.Security.Principal;
using deps.WebSocketSharp.Net;
using deps.WebSocketSharp.Net.WebSockets;

namespace deps.WebSocketSharp.Server
{
  /// <summary>
  /// Exposes a set of methods and properties used to define the behavior of
  /// a WebSocket service provided by the <see cref="WebSocketServer"/> or
  /// <see cref="HttpServer"/> class.
  /// </summary>
  /// <remarks>
  /// This class is an abstract class.
  /// </remarks>
  public abstract class WebSocketBehavior : IWebSocketSession
  {
    #region Private Fields

    private WebSocketContext                               _context;
    private Func<CookieCollection, CookieCollection, bool> _cookiesValidator;
    private bool                                           _emitOnPing;
    private Func<string, bool>                             _hostValidator;
    private string                                         _id;
    private bool                                           _ignoreExtensions;
    private bool                                           _noDelay;
    private Func<string, bool>                             _originValidator;
    private string                                         _protocol;
    private WebSocketSessionManager                        _sessions;
    private DateTime                                       _startTime;
    private WebSocket                                      _websocket;

    #endregion

    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketBehavior"/> class.
    /// </summary>
    protected WebSocketBehavior ()
    {
      _startTime = DateTime.MaxValue;
    }

    #endregion

    #region Protected Properties

    /// <summary>
    /// Gets the HTTP headers for a session.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the headers
    /// included in the WebSocket handshake request.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected NameValueCollection Headers {
      get {
        if (_context == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _context.Headers;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the communication is possible for
    /// a session.
    /// </summary>
    /// <value>
    /// <c>true</c> if the communication is possible; otherwise, <c>false</c>.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected bool IsAlive {
      get {
        if (_websocket == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _websocket.IsAlive;
      }
    }

    /// <summary>
    /// Gets the query string for a session.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="NameValueCollection"/> that contains the query
    ///   parameters included in the WebSocket handshake request.
    ///   </para>
    ///   <para>
    ///   An empty collection if not included.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected NameValueCollection QueryString {
      get {
        if (_context == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _context.QueryString;
      }
    }

    /// <summary>
    /// Gets the current state of the WebSocket interface for a session.
    /// </summary>
    /// <value>
    ///   <para>
    ///   One of the <see cref="WebSocketState"/> enum values.
    ///   </para>
    ///   <para>
    ///   It indicates the current state of the interface.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected WebSocketState ReadyState {
      get {
        if (_websocket == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _websocket.ReadyState;
      }
    }

    /// <summary>
    /// Gets the management function for the sessions in the service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions in
    /// the service.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected WebSocketSessionManager Sessions {
      get {
        if (_sessions == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _sessions;
      }
    }

    /// <summary>
    /// Gets the client information for a session.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="IPrincipal"/> instance that represents identity,
    ///   authentication, and security roles for the client.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if the client is not authenticated.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected IPrincipal User {
      get {
        if (_context == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _context.User;
      }
    }

    /// <summary>
    /// Gets the client endpoint for a session.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that represents the client
    /// IP address and port number.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The get operation is not available when the session has not started yet.
    /// </exception>
    protected System.Net.IPEndPoint UserEndPoint {
      get {
        if (_context == null) {
          var msg = "The get operation is not available.";

          throw new InvalidOperationException (msg);
        }

        return _context.UserEndPoint;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the delegate used to validate the HTTP cookies.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="T:System.Func{CookieCollection, CookieCollection, bool}"/>
    ///   delegate.
    ///   </para>
    ///   <para>
    ///   It represents the delegate called when the WebSocket interface
    ///   for a session validates the handshake request.
    ///   </para>
    ///   <para>
    ///   1st <see cref="CookieCollection"/> parameter passed to the delegate
    ///   contains the cookies to validate.
    ///   </para>
    ///   <para>
    ///   2nd <see cref="CookieCollection"/> parameter passed to the delegate
    ///   holds the cookies to send to the client.
    ///   </para>
    ///   <para>
    ///   The method invoked by the delegate must return <c>true</c>
    ///   if the cookies are valid.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public Func<CookieCollection, CookieCollection, bool> CookiesValidator {
      get {
        return _cookiesValidator;
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        _cookiesValidator = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket interface for
    /// a session emits the message event when it receives a ping.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the interface emits the message event when it receives
    ///   a ping; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public bool EmitOnPing {
      get {
        return _emitOnPing;
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        _emitOnPing = value;
      }
    }

    /// <summary>
    /// Gets or sets the delegate used to validate the Host header.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="T:System.Func{string, bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It represents the delegate called when the WebSocket interface
    ///   for a session validates the handshake request.
    ///   </para>
    ///   <para>
    ///   The <see cref="string"/> parameter passed to the delegate is
    ///   the value of the Host header.
    ///   </para>
    ///   <para>
    ///   The method invoked by the delegate must return <c>true</c>
    ///   if the header value is valid.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public Func<string, bool> HostValidator {
      get {
        return _hostValidator;
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        _hostValidator = value;
      }
    }

    /// <summary>
    /// Gets the unique ID of a session.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the unique ID of the session.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> when the session has not started yet.
    ///   </para>
    /// </value>
    public string ID {
      get {
        return _id;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket interface for
    /// a session ignores the Sec-WebSocket-Extensions header.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the interface ignores the extensions requested
    ///   from the client; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public bool IgnoreExtensions {
      get {
        return _ignoreExtensions;
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        _ignoreExtensions = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the underlying TCP socket of
    /// the WebSocket interface for a session disables a delay when send or
    /// receive buffer is not full.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the delay is disabled; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    /// <seealso cref="System.Net.Sockets.Socket.NoDelay"/>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public bool NoDelay {
      get {
        return _noDelay;
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        _noDelay = value;
      }
    }

    /// <summary>
    /// Gets or sets the delegate used to validate the Origin header.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="T:System.Func{string, bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It represents the delegate called when the WebSocket interface
    ///   for a session validates the handshake request.
    ///   </para>
    ///   <para>
    ///   The <see cref="string"/> parameter passed to the delegate is
    ///   the value of the Origin header or <see langword="null"/> if
    ///   the header is not present.
    ///   </para>
    ///   <para>
    ///   The method invoked by the delegate must return <c>true</c>
    ///   if the header value is valid.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public Func<string, bool> OriginValidator {
      get {
        return _originValidator;
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        _originValidator = value;
      }
    }

    /// <summary>
    /// Gets or sets the name of the WebSocket subprotocol for a session.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the name of the subprotocol.
    ///   </para>
    ///   <para>
    ///   The value specified for a set operation must be a token defined in
    ///   <see href="http://tools.ietf.org/html/rfc2616#section-2.2">
    ///   RFC 2616</see>.
    ///   </para>
    ///   <para>
    ///   The value is initialized if not requested.
    ///   </para>
    ///   <para>
    ///   The default value is an empty string.
    ///   </para>
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is not a token.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available when the session has already started.
    /// </exception>
    public string Protocol {
      get {
        return _websocket != null
               ? _websocket.Protocol
               : (_protocol ?? String.Empty);
      }

      set {
        if (_websocket != null) {
          var msg = "The set operation is not available.";

          throw new InvalidOperationException (msg);
        }

        if (value == null || value.Length == 0) {
          _protocol = null;

          return;
        }

        if (!value.IsToken ()) {
          var msg = "Not a token.";

          throw new ArgumentException (msg, "value");
        }

        _protocol = value;
      }
    }

    /// <summary>
    /// Gets the time that a session has started.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="DateTime"/> that represents the time that the session
    ///   has started.
    ///   </para>
    ///   <para>
    ///   <see cref="DateTime.MaxValue"/> when the session has not started yet.
    ///   </para>
    /// </value>
    public DateTime StartTime {
      get {
        return _startTime;
      }
    }

    #endregion

    #region Private Methods

    private string checkHandshakeRequest (WebSocketContext context)
    {
      if (_hostValidator != null) {
        if (!_hostValidator (context.Host)) {
          var msg = "The Host header is invalid.";

          return msg;
        }
      }

      if (_originValidator != null) {
        if (!_originValidator (context.Origin)) {
          var msg = "The Origin header is non-existent or invalid.";

          return msg;
        }
      }

      if (_cookiesValidator != null) {
        var req = context.CookieCollection;
        var res = context.WebSocket.CookieCollection;

        if (!_cookiesValidator (req, res)) {
          var msg = "The Cookie header is non-existent or invalid.";

          return msg;
        }
      }

      return null;
    }

    private void onClose (object sender, CloseEventArgs e)
    {
      if (_id == null)
        return;

      _sessions.Remove (_id);

      OnClose (e);
    }

    private void onError (object sender, ErrorEventArgs e)
    {
      OnError (e);
    }

    private void onMessage (object sender, MessageEventArgs e)
    {
      OnMessage (e);
    }

    private void onOpen (object sender, EventArgs e)
    {
      _id = _sessions.Add (this);

      if (_id == null) {
        _websocket.Close (CloseStatusCode.Away);

        return;
      }

      _startTime = DateTime.Now;

      OnOpen ();
    }

    #endregion

    #region Internal Methods

    internal void Start (
      WebSocketContext context,
      WebSocketSessionManager sessions
    )
    {
      _context = context;
      _sessions = sessions;

      _websocket = context.WebSocket;
      _websocket.CustomHandshakeRequestChecker = checkHandshakeRequest;

      if (_emitOnPing)
        _websocket.EmitOnPing = true;

      if (_ignoreExtensions)
        _websocket.IgnoreExtensions = true;

      if (_noDelay)
        _websocket.NoDelay = true;

      if (_protocol != null)
        _websocket.Protocol = _protocol;

      var waitTime = sessions.WaitTime;

      if (waitTime != _websocket.WaitTime)
        _websocket.WaitTime = waitTime;

      _websocket.OnClose += onClose;
      _websocket.OnError += onError;
      _websocket.OnMessage += onMessage;
      _websocket.OnOpen += onOpen;

      _websocket.Accept ();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Closes the WebSocket connection for a session.
    /// </summary>
    /// <remarks>
    /// This method does nothing when the current state of the WebSocket
    /// interface is Closing or Closed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The Close method is not available when the session has not started yet.
    /// </exception>
    protected void Close ()
    {
      if (_websocket == null) {
        var msg = "The Close method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Close ();
    }

    /// <summary>
    /// Closes the WebSocket connection for a session with the specified
    /// status code and reason.
    /// </summary>
    /// <remarks>
    /// This method does nothing when the current state of the WebSocket
    /// interface is Closing or Closed.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that specifies the status code indicating
    ///   the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1005 (no status) and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <para>
    ///   <paramref name="code"/> is less than 1000 or greater than 4999.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The size of <paramref name="reason"/> is greater than 123 bytes.
    ///   </para>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The Close method is not available when the session has not started yet.
    /// </exception>
    protected void Close (ushort code, string reason)
    {
      if (_websocket == null) {
        var msg = "The Close method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Close (code, reason);
    }

    /// <summary>
    /// Closes the WebSocket connection for a session with the specified
    /// status code and reason.
    /// </summary>
    /// <remarks>
    /// This method does nothing when the current state of the WebSocket
    /// interface is Closing or Closed.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the status code indicating the reason for the close.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is an undefined enum value.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.NoStatus"/> and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="reason"/> is greater than 123 bytes.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The Close method is not available when the session has not started yet.
    /// </exception>
    protected void Close (CloseStatusCode code, string reason)
    {
      if (_websocket == null) {
        var msg = "The Close method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Close (code, reason);
    }

    /// <summary>
    /// Closes the WebSocket connection for a session asynchronously.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing when the current state of the WebSocket
    ///   interface is Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The CloseAsync method is not available when the session has not
    /// started yet.
    /// </exception>
    protected void CloseAsync ()
    {
      if (_websocket == null) {
        var msg = "The CloseAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.CloseAsync ();
    }

    /// <summary>
    /// Closes the WebSocket connection for a session asynchronously with
    /// the specified status code and reason.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing when the current state of the WebSocket
    ///   interface is Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that specifies the status code indicating
    ///   the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1005 (no status) and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <para>
    ///   <paramref name="code"/> is less than 1000 or greater than 4999.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The size of <paramref name="reason"/> is greater than 123 bytes.
    ///   </para>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The CloseAsync method is not available when the session has not
    /// started yet.
    /// </exception>
    protected void CloseAsync (ushort code, string reason)
    {
      if (_websocket == null) {
        var msg = "The CloseAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.CloseAsync (code, reason);
    }

    /// <summary>
    /// Closes the WebSocket connection for a session asynchronously with
    /// the specified status code and reason.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing when the current state of the WebSocket
    ///   interface is Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the status code indicating the reason for the close.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is an undefined enum value.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.NoStatus"/> and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="reason"/> is greater than 123 bytes.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The CloseAsync method is not available when the session has not
    /// started yet.
    /// </exception>
    protected void CloseAsync (CloseStatusCode code, string reason)
    {
      if (_websocket == null) {
        var msg = "The CloseAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.CloseAsync (code, reason);
    }

    /// <summary>
    /// Called when the WebSocket connection for a session has been closed.
    /// </summary>
    /// <param name="e">
    /// A <see cref="CloseEventArgs"/> that represents the event data passed
    /// from a <see cref="WebSocket.OnClose"/> event.
    /// </param>
    protected virtual void OnClose (CloseEventArgs e)
    {
    }

    /// <summary>
    /// Called when the WebSocket interface for a session gets an error.
    /// </summary>
    /// <param name="e">
    /// A <see cref="ErrorEventArgs"/> that represents the event data passed
    /// from a <see cref="WebSocket.OnError"/> event.
    /// </param>
    protected virtual void OnError (ErrorEventArgs e)
    {
    }

    /// <summary>
    /// Called when the WebSocket interface for a session receives a message.
    /// </summary>
    /// <param name="e">
    /// A <see cref="MessageEventArgs"/> that represents the event data passed
    /// from a <see cref="WebSocket.OnMessage"/> event.
    /// </param>
    protected virtual void OnMessage (MessageEventArgs e)
    {
    }

    /// <summary>
    /// Called when the WebSocket connection for a session has been established.
    /// </summary>
    protected virtual void OnOpen ()
    {
    }

    /// <summary>
    /// Sends a ping to the client for a session.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the send has successfully done and a pong has been
    /// received within a time; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The Ping method is not available when the session has not started yet.
    /// </exception>
    protected bool Ping ()
    {
      if (_websocket == null) {
        var msg = "The Ping method is not available.";

        throw new InvalidOperationException (msg);
      }

      return _websocket.Ping ();
    }

    /// <summary>
    /// Sends a ping with the specified message to the client for a session.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the send has successfully done and a pong has been
    /// received within a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    ///   <para>
    ///   A <see cref="string"/> that specifies the message to send.
    ///   </para>
    ///   <para>
    ///   Its size must be 125 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> could not be UTF-8-encoded.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="message"/> is greater than 125 bytes.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The Ping method is not available when the session has not started yet.
    /// </exception>
    protected bool Ping (string message)
    {
      if (_websocket == null) {
        var msg = "The Ping method is not available.";

        throw new InvalidOperationException (msg);
      }

      return _websocket.Ping (message);
    }

    /// <summary>
    /// Sends the specified data to the client for a session.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that specifies the binary data to send.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The Send method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The Send method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void Send (byte[] data)
    {
      if (_websocket == null) {
        var msg = "The Send method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Send (data);
    }

    /// <summary>
    /// Sends the specified file to the client for a session.
    /// </summary>
    /// <param name="fileInfo">
    ///   <para>
    ///   A <see cref="FileInfo"/> that specifies the file to send.
    ///   </para>
    ///   <para>
    ///   The file is sent as the binary data.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   The file does not exist.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The file could not be opened.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The Send method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The Send method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void Send (FileInfo fileInfo)
    {
      if (_websocket == null) {
        var msg = "The Send method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Send (fileInfo);
    }

    /// <summary>
    /// Sends the specified data to the client for a session.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that specifies the text data to send.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> could not be UTF-8-encoded.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The Send method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The Send method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void Send (string data)
    {
      if (_websocket == null) {
        var msg = "The Send method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Send (data);
    }

    /// <summary>
    /// Sends the data from the specified stream instance to the client for
    /// a session.
    /// </summary>
    /// <param name="stream">
    ///   <para>
    ///   A <see cref="Stream"/> instance from which to read the data to send.
    ///   </para>
    ///   <para>
    ///   The data is sent as the binary data.
    ///   </para>
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that specifies the number of bytes to send.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="stream"/> cannot be read.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than 1.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   No data could be read from <paramref name="stream"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The Send method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The Send method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void Send (Stream stream, int length)
    {
      if (_websocket == null) {
        var msg = "The Send method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.Send (stream, length);
    }

    /// <summary>
    /// Sends the specified data to the client for a session asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that specifies the binary data to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It specifies the delegate called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the delegate is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The SendAsync method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The SendAsync method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void SendAsync (byte[] data, Action<bool> completed)
    {
      if (_websocket == null) {
        var msg = "The SendAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends the specified file to the client for a session asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="fileInfo">
    ///   <para>
    ///   A <see cref="FileInfo"/> that specifies the file to send.
    ///   </para>
    ///   <para>
    ///   The file is sent as the binary data.
    ///   </para>
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It specifies the delegate called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the delegate is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   The file does not exist.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The file could not be opened.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The SendAsync method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The SendAsync method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void SendAsync (FileInfo fileInfo, Action<bool> completed)
    {
      if (_websocket == null) {
        var msg = "The SendAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.SendAsync (fileInfo, completed);
    }

    /// <summary>
    /// Sends the specified data to the client for a session asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that specifies the text data to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It specifies the delegate called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the delegate is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> could not be UTF-8-encoded.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The SendAsync method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The SendAsync method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void SendAsync (string data, Action<bool> completed)
    {
      if (_websocket == null) {
        var msg = "The SendAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends the data from the specified stream instance to the client for
    /// a session asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="stream">
    ///   <para>
    ///   A <see cref="Stream"/> instance from which to read the data to send.
    ///   </para>
    ///   <para>
    ///   The data is sent as the binary data.
    ///   </para>
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that specifies the number of bytes to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   It specifies the delegate called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the delegate is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="stream"/> cannot be read.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than 1.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   No data could be read from <paramref name="stream"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The SendAsync method is not available when the session has not
    ///   started yet.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The SendAsync method is not available when the current state of
    ///   the WebSocket interface is not Open.
    ///   </para>
    /// </exception>
    protected void SendAsync (Stream stream, int length, Action<bool> completed)
    {
      if (_websocket == null) {
        var msg = "The SendAsync method is not available.";

        throw new InvalidOperationException (msg);
      }

      _websocket.SendAsync (stream, length, completed);
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Gets the WebSocket interface for a session.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="WebSocketSharp.WebSocket"/> that represents
    ///   the WebSocket interface.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> when the session has not started yet.
    ///   </para>
    /// </value>
    WebSocket IWebSocketSession.WebSocket {
      get {
        return _websocket;
      }
    }

    #endregion
  }
}
