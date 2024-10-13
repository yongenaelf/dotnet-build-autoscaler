"use strict";

var connection = new signalR.HubConnectionBuilder()
  .withUrl("/buildOutputHub")
  .build();

//Disable the send button until connection is established.
document.getElementById("sendButton").disabled = true;

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
    document.getElementById("sendButton").disabled = false;
  })
  .catch(function (err) {
    return console.error(err.toString());
  });

document
  .getElementById("sendButton")
  .addEventListener("click", function (event) {
    var message = document.getElementById("messageInput").value;
    connection.invoke("SendMessage", message).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
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
      })
      .catch((error) => {
        console.error(
          "There has been a problem with your fetch operation:",
          error
        );
      });
  });
});
