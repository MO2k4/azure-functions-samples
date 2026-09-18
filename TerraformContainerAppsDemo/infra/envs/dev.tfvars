subscription_id = "00000000-0000-0000-0000-000000000000"

workload      = "orders"
environment   = "dev"
location      = "westeurope"
address_space = "10.60.0.0/16"

cost_center = "GAZE"
owner       = "AZE"
project     = "orders"

image_tag    = "1.4.2-dev"
min_replicas = 0
max_replicas = 3

log_retention_days = 30

# dev carries the same profile shape as prod, scaled down. Adding the first workload profile to
# an environment that has none is a recreate, so a dev environment without profiles cannot be
# used to rehearse a prod change that has them.
dedicated_workload_profiles = {
  D4 = {
    profile_type  = "D4"
    minimum_count = 0
    maximum_count = 1
  }
}

# orders_api_key has no default and is deliberately absent here. Supply it as
# TF_VAR_orders_api_key from the pipeline, not from a file in git.
