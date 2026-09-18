subscription_id = "00000000-0000-0000-0000-000000000000"

workload      = "orders"
environment   = "prod"
location      = "westeurope"
address_space = "10.70.0.0/16"

cost_center = "GAZE"
owner       = "AZE"
project     = "orders"

image_tag    = "1.4.2"
min_replicas = 2
max_replicas = 30

log_retention_days = 90

dedicated_workload_profiles = {
  D4 = {
    profile_type  = "D4"
    minimum_count = 1
    maximum_count = 5
  }
}

# orders_api_key has no default and is deliberately absent here. Supply it as
# TF_VAR_orders_api_key from the pipeline, not from a file in git.
