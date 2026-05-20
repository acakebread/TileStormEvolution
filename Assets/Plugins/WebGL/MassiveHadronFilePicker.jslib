mergeInto(LibraryManager.library, {
  MassiveHadron_OpenFilePicker: function (titlePtr, acceptPtr, allowMultiple, allowDirectory, receiverPtr, onFilePtr, onCompletePtr) {
    var title = UTF8ToString(titlePtr);
    var accept = UTF8ToString(acceptPtr);
    var receiver = UTF8ToString(receiverPtr);
    var onFile = UTF8ToString(onFilePtr);
    var onComplete = UTF8ToString(onCompletePtr);

    var input = document.createElement('input');
    input.type = 'file';
    input.multiple = !!allowMultiple;
    input.style.display = 'none';

    if (allowDirectory) {
      input.setAttribute('webkitdirectory', '');
      input.setAttribute('directory', '');
    }

    if (accept && accept.length > 0) {
      input.accept = accept;
    }

    input.title = title || 'Select File';

    input.onchange = function () {
      var files = Array.from(input.files || []);
      if (input.parentNode) {
        input.parentNode.removeChild(input);
      }
      if (!files.length) {
        SendMessage(receiver, onComplete, '');
        return;
      }

      var remaining = files.length;
      files.forEach(function (file) {
        var reader = new FileReader();
        reader.onerror = function () {
          remaining -= 1;
          if (remaining <= 0) {
            SendMessage(receiver, onComplete, '');
          }
        };
        reader.onload = function (ev) {
          var dataUrl = ev.target.result || '';
          var comma = dataUrl.indexOf(',');
          var base64 = comma >= 0 ? dataUrl.substring(comma + 1) : '';
          var payload = JSON.stringify({
            name: file.name || '',
            relativePath: file.webkitRelativePath || '',
            base64: base64
          });

          SendMessage(receiver, onFile, payload);

          remaining -= 1;
          if (remaining <= 0) {
            SendMessage(receiver, onComplete, '');
          }
        };
        reader.readAsDataURL(file);
      });
    };

    document.body.appendChild(input);
    input.click();
  },

  MassiveHadron_OpenDirectoryPicker: function (titlePtr, receiverPtr, onFilePtr, onCompletePtr) {
    var title = UTF8ToString(titlePtr);
    var receiver = UTF8ToString(receiverPtr);
    var onFile = UTF8ToString(onFilePtr);
    var onComplete = UTF8ToString(onCompletePtr);

    var input = document.createElement('input');
    input.type = 'file';
    input.multiple = false;
    input.setAttribute('webkitdirectory', '');
    input.setAttribute('directory', '');
    input.style.display = 'none';
    input.title = title || 'Select Folder';

    input.onchange = function () {
      var files = Array.from(input.files || []);
      if (input.parentNode) {
        input.parentNode.removeChild(input);
      }

      if (!files.length) {
        SendMessage(receiver, onComplete, '');
        return;
      }

      var remaining = files.length;
      files.forEach(function (file) {
        var reader = new FileReader();
        reader.onerror = function () {
          remaining -= 1;
          if (remaining <= 0) {
            SendMessage(receiver, onComplete, '');
          }
        };
        reader.onload = function (ev) {
          var dataUrl = ev.target.result || '';
          var comma = dataUrl.indexOf(',');
          var base64 = comma >= 0 ? dataUrl.substring(comma + 1) : '';
          var payload = JSON.stringify({
            name: file.name || '',
            relativePath: file.webkitRelativePath || '',
            base64: base64
          });

          SendMessage(receiver, onFile, payload);

          remaining -= 1;
          if (remaining <= 0) {
            SendMessage(receiver, onComplete, '');
          }
        };
        reader.readAsDataURL(file);
      });
    };

    document.body.appendChild(input);
    input.click();
  },

  MassiveHadron_DownloadText: function (filenamePtr, textPtr, mimeTypePtr) {
    var filename = UTF8ToString(filenamePtr);
    var text = UTF8ToString(textPtr);
    var mimeType = UTF8ToString(mimeTypePtr) || 'text/plain;charset=utf-8';

    var blob = new Blob([text], { type: mimeType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename || 'download.txt';
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  },

  MassiveHadron_DownloadBytes: function (filenamePtr, dataPtr, length, mimeTypePtr) {
    var filename = UTF8ToString(filenamePtr);
    var mimeType = UTF8ToString(mimeTypePtr) || 'application/octet-stream';
    var bytes = HEAPU8.slice(dataPtr, dataPtr + length);

    var blob = new Blob([bytes], { type: mimeType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename || 'download.bin';
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  },

  MassiveHadron_SyncPersistentData: function (populate, receiverPtr, onCompletePtr) {
    var receiver = UTF8ToString(receiverPtr);
    var onComplete = UTF8ToString(onCompletePtr);

    function finish(success, message) {
      var payload = success ? '1' : '0';
      if (message && message.length > 0) {
        payload += '|' + message;
      }
      SendMessage(receiver, onComplete, payload);
    }

    try {
      if (typeof FS === 'undefined' || typeof FS.syncfs !== 'function') {
        finish(false, 'FS.syncfs unavailable');
        return;
      }

      FS.syncfs(!!populate, function (err) {
        if (err) {
          finish(false, err.message || String(err));
          return;
        }

        finish(true, '');
      });
    } catch (ex) {
      finish(false, ex && ex.message ? ex.message : String(ex));
    }
  },

  MassiveHadron_GetQueryParameter: function (namePtr) {
    var name = UTF8ToString(namePtr);
    if (!name || name.length === 0) {
      return stringToNewUTF8('');
    }

    function readFromLocation(locationLike) {
      if (!locationLike) {
        return '';
      }

      var search = locationLike.search || '';
      if (!search && locationLike.href) {
        var question = locationLike.href.indexOf('?');
        if (question >= 0) {
          search = locationLike.href.substring(question);
          var hash = search.indexOf('#');
          if (hash >= 0) {
            search = search.substring(0, hash);
          }
        }
      }

      if (!search || search.length <= 1) {
        return '';
      }

      var params = new URLSearchParams(search);
      return params.get(name) || '';
    }

    var value = '';
    try {
      value = readFromLocation(window.location);
      if (!value && window.parent && window.parent !== window) {
        value = readFromLocation(window.parent.location);
      }
      if (!value && name === 'map') {
        var stored = window.localStorage ? window.localStorage.getItem('TileStormLaunchMap') : '';
        var storedAt = window.localStorage ? parseInt(window.localStorage.getItem('TileStormLaunchMapTime') || '0', 10) : 0;
        var isFresh = storedAt > 0 && (Date.now() - storedAt) < (10 * 60 * 1000);
        if (stored && isFresh) {
          value = stored;
        }
        if (window.localStorage) {
          window.localStorage.removeItem('TileStormLaunchMap');
          window.localStorage.removeItem('TileStormLaunchMapTime');
        }
      }
    } catch (ex) {
      value = '';
    }

    return stringToNewUTF8(value);
  }
});
