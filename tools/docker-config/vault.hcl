log_level = "Trace"
cluster_name = "burcin"
ui = true

listener "tcp" {
  address = "0.0.0.0:8200"
  cluster_address = "0.0.0.0:8201"
  tls_disable = 1
}

storage "file" {
  path = "/vault/file"
}
