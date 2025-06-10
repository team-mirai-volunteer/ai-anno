output "instance_name" {
  description = "The name of the database instance"
  value       = google_sql_database_instance.main.name
}

output "instance_connection_name" {
  description = "The connection name of the database instance"
  value       = google_sql_database_instance.main.connection_name
}

output "private_ip_address" {
  description = "The private IP address of the database instance"
  value       = google_sql_database_instance.main.private_ip_address
}

output "database_name" {
  description = "The name of the database"
  value       = google_sql_database.dify.name
}

output "database_user" {
  description = "The database user"
  value       = google_sql_user.dify.name
}

output "database_password" {
  description = "The database password"
  value       = random_password.db_password.result
  sensitive   = true
}