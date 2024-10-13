# dotnet build autoscaler

## Development

### Using docker compose

First, install Docker and Docker Compose.

Then, run:

```bash
docker compose up -d
```

In a separate terminal, run:

```bash
dotnet watch --project BuildJobApi -lp https
```

In a separate terminal, run:

```bash
dotnet watch --project KafkaConsumer
```

### Clean up

After you are done, you can stop with `Ctrl+C`.
