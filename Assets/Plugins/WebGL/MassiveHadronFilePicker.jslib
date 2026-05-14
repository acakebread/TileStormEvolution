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
  }
});
