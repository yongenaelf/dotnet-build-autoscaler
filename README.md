# dotnet build autoscaler

## Development

First, install Skaffold:

```bash
brew install skaffold
```

Then, run:

```bash
kubectl config use-context docker-desktop 
skaffold dev --port-forward
```

Visit http://localhost:8080/swagger/index.html to see the API documentation.

### Clean up

After you are done, you can stop with `Ctrl+C`.