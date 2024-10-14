"use strict";

var connection = new signalR.HubConnectionBuilder()
  .withUrl("/buildOutputHub")
  .build();

connection.on("ReceiveMessage", function (message) {
  var li = document.createElement("li");
  document.getElementById("messagesList").appendChild(li);
  // We can assign user-supplied strings to an element's textContent because it
  // is not interpreted as markup. If you're assigning in any other way, you
  // should be aware of possible script injection concerns.
  li.textContent = `${message}`;
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
        connection.invoke("AddToGroup", data.jobId).catch(function (err) {
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
