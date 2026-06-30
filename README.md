# AWS Examples

> Practical AWS examples built in C# and Python — Lambda functions, EC2 automation, Security Group management, and cloud infrastructure utilities.

![AWS](https://img.shields.io/badge/AWS-Lambda%20%7C%20EC2%20%7C%20SQS-orange)
![Languages](https://img.shields.io/badge/Languages-C%23%20%7C%20Python-blue)
![Level](https://img.shields.io/badge/Level-Practical%20Examples-green)

---

## Overview

A collection of production-ready AWS automation scripts and Lambda functions. Each example is self-contained, focused on a single AWS problem, and built to be adapted for real workloads.

---

## Examples

### Lambda Functions

| Project | Description | Runtime |
|---|---|---|
| [AWS-Lambda-startEC2](https://github.com/LucasRios/AWS-Lambda-startEC2) | Starts EC2 instances on a schedule or trigger | Python |
| [AWS-Lambda-stopEC2](https://github.com/LucasRios/AWS-Lambda-stopEC2) | Stops EC2 instances to reduce costs during off-hours | Python |
| [AWSInfiniteBedtimeStory](https://github.com/LucasRios/AWSInfiniteBedtimeStory) | Generative AI story pipeline using AWS Bedrock | Python |

### Infrastructure & Networking

| Project | Description | Runtime |
|---|---|---|
| [AWS-Atualizar-IP-Security-Group](https://github.com/LucasRios/AWS-Atualizar-IP-Security-Group) | Auto-updates Security Group rules when public IP changes — useful for dynamic IP connections to RDS | C# |

---

## Patterns Covered

- **Scheduled Lambda** — EventBridge triggers for EC2 start/stop
- **Dynamic IP management** — detect IP change, update AWS Security Group via SDK
- **Generative AI on AWS** — Bedrock integration for content generation pipelines
- **Cost optimization** — automated instance lifecycle management

---

## Tech Stack

- **AWS Lambda** — serverless compute
- **Amazon EC2** — virtual machines
- **Amazon Bedrock** — generative AI
- **AWS Security Groups** — network access control
- **AWS SDK for .NET / Boto3** — programmatic AWS access

---

## License

MIT
