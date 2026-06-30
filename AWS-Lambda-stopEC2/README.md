# AWS Lambda — Stop EC2

Lambda function that stops one or more EC2 instances from a list of instance IDs.

## Event payload

```json
{
  "instance_ids": ["i-1234567890abcdef0", "i-0987654321fedcba0"]
}
```

## IAM permissions required

Attach this inline policy to the Lambda execution role:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ec2:StopInstances",
        "ec2:DescribeInstances"
      ],
      "Resource": "*"
    }
  ]
}
```

## Environment variables

| Variable | Description | Default |
|---|---|---|
| `AWS_TARGET_REGION` | AWS region where the instances are located | `us-east-1` |

## Typical trigger

Schedule via **EventBridge** (e.g. every weekday at 20:00):

```
cron(0 23 ? * MON-FRI *)   # 20:00 BRT = 23:00 UTC
```

## Response

```json
{
  "statusCode": 200,
  "body": {
    "message": "Stop command sent to 2 instance(s).",
    "state_changes": [
      { "instance_id": "i-1234567890abcdef0", "previous_state": "running", "current_state": "stopping" }
    ]
  }
}
```
