"""
AWS Lambda — Stop EC2 Instances
================================
Receives a list of EC2 instance IDs via the event payload and stops them.

Expected event payload:
    {
        "instance_ids": ["i-1234567890abcdef0", "i-0987654321fedcba0"]
    }

The Lambda must have an IAM role with the following minimum permission:
    {
        "Effect": "Allow",
        "Action": ["ec2:StopInstances", "ec2:DescribeInstances"],
        "Resource": "*"
    }

Typical use case: scheduled stop via EventBridge to cut costs during off-hours.
No credentials are stored in this function — the SDK uses the Lambda execution role.
"""

import boto3
import json
import os


def lambda_handler(event, context):
    """
    Entry point called by AWS Lambda when the function is invoked.

    Parameters
    ----------
    event : dict
        The input data passed to the Lambda. Must contain 'instance_ids',
        a list of EC2 instance ID strings (e.g. ["i-abc123", "i-def456"]).
    context : LambdaContext
        Runtime information provided by AWS (function name, timeout, etc.).
        Not used directly here but required by the Lambda signature.

    Returns
    -------
    dict
        A response object with HTTP statusCode and a JSON body describing
        which instances were stopped and their new state transitions.
    """

    # --- 1. Validate input ---------------------------------------------------

    # Extract the list of instance IDs from the event.
    # An empty list is treated as a no-op rather than an error.
    instance_ids = event.get("instance_ids", [])

    if not instance_ids:
        return _response(400, {"message": "No instance_ids provided in the event payload."})

    if not isinstance(instance_ids, list):
        return _response(400, {"message": "'instance_ids' must be a list of strings."})

    # --- 2. Determine AWS region ---------------------------------------------

    # The region can be overridden via an environment variable set on the Lambda
    # (Configuration → Environment variables → AWS_TARGET_REGION).
    # Falls back to us-east-1 if not set.
    region = os.environ.get("AWS_TARGET_REGION", "us-east-1")

    # --- 3. Create EC2 client ------------------------------------------------

    # boto3 automatically picks up the Lambda execution role credentials.
    # No access key or secret key should ever be hardcoded here.
    ec2 = boto3.client("ec2", region_name=region)

    # --- 4. Stop instances ---------------------------------------------------

    print(f"Stopping instances: {instance_ids} in region {region}")

    try:
        response = ec2.stop_instances(InstanceIds=instance_ids)
    except ec2.exceptions.ClientError as e:
        # Covers cases like invalid instance IDs, permission errors,
        # or trying to stop an already-stopped/terminated instance.
        error_message = str(e)
        print(f"ERROR calling stop_instances: {error_message}")
        return _response(500, {"message": "Failed to stop instances.", "error": error_message})

    # --- 5. Build result summary ---------------------------------------------

    # The response from stop_instances contains a list of StateChange objects,
    # each describing the previous and current state of the instance.
    state_changes = [
        {
            "instance_id": change["InstanceId"],
            "previous_state": change["PreviousState"]["Name"],
            "current_state": change["CurrentState"]["Name"],
        }
        for change in response.get("StoppingInstances", [])
    ]

    print(f"State changes: {state_changes}")

    return _response(200, {
        "message": f"Stop command sent to {len(state_changes)} instance(s).",
        "state_changes": state_changes,
    })


def _response(status_code, body):
    """
    Helper that wraps a dict body into a standard Lambda HTTP response.

    Parameters
    ----------
    status_code : int
        HTTP-style status code (200 for success, 400/500 for errors).
    body : dict
        The data to return as JSON.
    """
    return {
        "statusCode": status_code,
        "body": json.dumps(body),
    }
