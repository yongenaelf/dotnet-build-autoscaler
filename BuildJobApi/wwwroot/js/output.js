"use strict";

var term = new Terminal({
  cols: 200,
});

term.open(document.getElementById("terminal"));

var connection = new signalR.HubConnectionBuilder()
  .withUrl("/buildOutputHub")
  .build();

connection.on("ReceiveMessage", function (message) {
  term.write(message + "\r\n");
});

connection
  .start()
  .then(function () {
    console.log("Connection started");
  })
  .catch(function (err) {
    return console.error(err.toString());
  });

Array.from(document.getElementsByTagName("button")).forEach(function (button) {
  button.addEventListener("click", function (event) {
    var command = event.target.id;
    var fileUpload = document.getElementById("fileUpload");
    var file = fileUpload.files[0];

    if (!file) {
      alert("Please select a file to upload.");
      return;
    }

    // Create a new FormData object.
    var formData = new FormData();

    // Add the file to the request.
    formData.append("file", file, file.name);

    connection.invoke("AddToGroup", crypto.randomUUID()).catch(function (err) {
      return console.error(err.toString());
    });

    // Set up the request using fetch.
    fetch("/upload?command=" + command, {
      method: "POST",
      body: formData,
    })
      .then((response) => {
        if (!response.ok) {
          throw new Error("Network response was not ok");
        }
        return response.json();
      })
      .then((data) => {
        console.log(data);
        term.clear();
        connection
          .invoke("StartBuild", data.jobId, command)
          .catch(function (err) {
            return console.error(err.toString());
          });
      })
      .catch((error) => {
        console.error(
          "There has been a problem with your fetch operation:",
          error
        );
      });
  });
});
