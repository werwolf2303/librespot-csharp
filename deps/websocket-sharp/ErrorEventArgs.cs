#region License
/*
 * ErrorEventArgs.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2022 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Frank Razenberg <frank@zzattack.org>
 */
#endregion

using System;

namespace deps.WebSocketSharp
{
  /// <summary>
  /// Represents the event data for the <see cref="WebSocket.OnError"/> event.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   The error event occurs when the <see cref="WebSocket"/> interface
  ///   gets an error.
  ///   </para>
  ///   <para>
  ///   If you would like to get the error message, you should access
  ///   the <see cref="ErrorEventArgs.Message"/> property.
  ///   </para>
  ///   <para>
  ///   If the error is due to an exception, you can get it by accessing
  ///   the <see cref="ErrorEventArgs.Exception"/> property.
  ///   </para>
  /// </remarks>
  public class ErrorEventArgs : EventArgs
  {
    #region Private Fields

    private Exception _exception;
    private string    _message;

    #endregion

    #region Internal Constructors

    internal ErrorEventArgs (string message)
      : this (message, null)
    {
    }

    internal ErrorEventArgs (string message, Exception exception)
    {
      _message = message;
      _exception = exception;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the exception that caused the error.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <see cref="System.Exception"/> instance that represents
    ///   the cause of the error.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not present.
    ///   </para>
    /// </value>
    public Exception Exception {
      get {
        return _exception;
      }
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the error message.
    /// </value>
    public string Message {
      get {
        return _message;
      }
    }

    #endregion
  }
}
