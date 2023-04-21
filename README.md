# Lasso
A usage quota wrangler.

Lasso helps keep track of any kind of quotas or metrics by incrementing or decrementing usage. It stores usage metrics, separated by a specified context, as numbers in a Redis Hashset for named resources. This keeps all usage metrics for a particular context together.

Lasso manages the scope of the usage data through customizable Redis key builders. These typically will be time-based, such as daily or monthly, but are completely customizable for any need.

Lasso does not modify the behavior of your application or prescribe any outcomes for crossing usage thresholds. Lasso is nothing more than a lightweight record keeper.

Lasso is designed to be asynchronous, thread safe, and fast. Calls to the UsageManager class are asynchronous and directly result in calls to Redis. Calls to the BatchedUsageManager class are thread safe.
