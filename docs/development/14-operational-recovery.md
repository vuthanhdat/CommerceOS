# Operational diagnostics and recovery

`python tools/commerceos.py recovery-inspect --instance 0001` lists queues belonging only to the selected LocalStack task instance, including pending counts and redrive configuration. It is read-only and operational evidence is never business authority.

For a confirmed poison-message recovery, `recovery-redrive` requires explicit source/destination queue URLs from the same task prefix and a bounded rate. Redrive preserves the original message/event identity; consumers must therefore remain idempotent. Provider `OutcomeUnknown` is recovered through the Payments reconciliation contract, never through this tool or a direct persistence edit.

LocalStack support for `start-message-move-task` can vary by image edition/version. If unavailable, record the limitation and use the consumer's idempotent replay fixture; do not mutate source-domain state to make a queue appear healthy.
