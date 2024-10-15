# dotnet build autoscaler

```mermaid
sequenceDiagram
    participant Client
    participant BuildJobAPI as Build Job API
    participant VirusScan as Virus Scan Service
    participant ObjectStorage as Object Storage
    participant Kafka
    participant KEDA
    participant Kubernetes as Worker Service

    Client->>BuildJobAPI: Upload job request
    BuildJobAPI->>BuildJobAPI: Generate GUID for job
    BuildJobAPI->>VirusScan: Check file for viruses
    alt No virus detected
        VirusScan->>BuildJobAPI: File is clean
        BuildJobAPI->>ObjectStorage: Save uploaded file by jobId
        BuildJobAPI->>Client: Return job GUID (jobId)
    else Virus detected
        VirusScan->>BuildJobAPI: Virus detected
        BuildJobAPI->>Client: Reject upload
    end

    Client->>BuildJobAPI: Request to join SignalR group with jobId
    BuildJobAPI->>Client: Confirm group subscription

    Client->>BuildJobAPI: Invoke build process with jobId
    BuildJobAPI->>Kafka: Publish jobId to Kafka

    Kafka->>KEDA: Notify about new job in queue
    KEDA->>Kubernetes: Scale up worker services

    Kubernetes->>ObjectStorage: Retrieve file by jobId
    Kubernetes->>BuildJobAPI: Complete job processing

    Kubernetes->>KEDA: Job finished processing
    KEDA->>Kubernetes: Scale down worker services

    BuildJobAPI->>Client: Notify job completion via SignalR (jobId)
```

## Development

> [!TIP]
> If you are using VS Code, you can press `F1` and type `Tasks: Run Task` to see the available tasks.
> "Run all services in watch mode" will start docker compose, then start all services in watch mode.

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
dotnet watch --project BuildJobWorker
```

### Clean up

After you are done, you can stop with `Ctrl+C`.

To remove docker containers, run:

```bash
docker compose down
```
