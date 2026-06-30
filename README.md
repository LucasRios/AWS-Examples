# AWS Examples

> Practical AWS examples built in C# and Python — Lambda functions, EC2 automation, Security Group management, and cloud infrastructure utilities.

![AWS](https://img.shields.io/badge/AWS-Lambda%20%7C%20EC2%20%7C%20SQS-orange)
![Languages](https://img.shields.io/badge/Languages-C%23%20%7C%20Python-blue)
![Level](https://img.shields.io/badge/Level-Practical%20Examples-green)

---

## Overview

A collection of production-ready AWS automation scripts and Lambda functions. Each example is self-contained, focused on a single AWS problem, and built to be adapted for real workloads.

---

## Project Structure

```
AWS-Examples/
├── AWS-Lambda-startEC2/   ← Python — Lambda to start EC2 instances from a list
├── AWS-Lambda-stopEC2/    ← Python — Lambda to stop EC2 instances from a list
├── infinite-bedtime-story/← Python / React — AWS Bedrock generative story pipeline
└── SW_AWS_IP_UPDATE/      ← C# — monitors public IP, updates Security Group rules
```

---

## Projects

### Lambda Functions (Python / Boto3)

| Project | Description |
|---|---|
| [AWS-Lambda-startEC2](AWS-Lambda-startEC2/) | Starts a list of EC2 instances — accepts instance IDs via event payload. Trigger via EventBridge schedule or manual invoke |
| [AWS-Lambda-stopEC2](AWS-Lambda-stopEC2/) | Stops a list of EC2 instances — same interface as start. Typical use: cut costs during off-hours |
| [infinite-bedtime-story](infinite-bedtime-story/) | Generative AI story pipeline using AWS Bedrock (Nova text + Polly audio + Nova Canvas images) |

### Infrastructure & Networking (C# / AWS SDK for .NET)

| Project | Description |
|---|---|
| [SW_AWS_IP_UPDATE](SW_AWS_IP_UPDATE/) | Monitors the machine's public IP and auto-updates AWS Security Group ingress rules when it changes. Sends a SendGrid email notification on each update |

---

## Getting Started

**Lambda functions** require Python 3.12+ and an AWS account. Deploy via the AWS Console or CLI:

```bash
# Package and deploy a Lambda manually
cd AWS-Lambda-startEC2
zip -r function.zip lambda_function.py
aws lambda create-function \
  --function-name StartEC2Instances \
  --runtime python3.12 \
  --handler lambda_function.lambda_handler \
  --role arn:aws:iam::ACCOUNT_ID:role/LambdaEC2Role \
  --zip-file fileb://function.zip
```

**SW_AWS_IP_UPDATE** requires .NET Framework 4.6.1 and Visual Studio. Copy `swconfigIP.ini.example` to `swconfigIP.ini` and fill in your credentials before building.

---

## Patterns Covered

- **Scheduled Lambda** — EventBridge triggers for EC2 start/stop based on a cron expression
- **Dynamic IP management** — detect public IP change, revoke old Security Group rule, authorize new IP
- **Generative AI on AWS** — Bedrock integration (text, voice, image) in a full-stack Python pipeline
- **Cost optimization** — automated EC2 lifecycle management to avoid paying for idle instances

## Security Notes

- Lambda functions use the **IAM execution role** — no credentials stored in code
- `SW_AWS_IP_UPDATE` reads AWS credentials and SendGrid key from `swconfigIP.ini` — **never hardcoded**
- `swconfigIP.ini` is listed in `.gitignore` and must never be committed
- Use `swconfigIP.ini.example` as the template

## Tech Stack

- **AWS Lambda** — serverless compute (Python 3.12+)
- **Amazon EC2** — virtual machines
- **Amazon Bedrock** — generative AI (Nova, Polly)
- **AWS Security Groups** — network access control
- **Boto3** — AWS SDK for Python
- **AWS SDK for .NET** — C# integration (SW_AWS_IP_UPDATE)
- **SendGrid** — transactional email notification

---

## License

MIT
