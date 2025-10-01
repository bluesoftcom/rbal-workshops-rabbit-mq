locals {
  final_name = var.name

  per_user_sql = [
    for u in var.users : <<-SQL
      IF DB_ID(N'${u.name}') IS NULL
      BEGIN
        DECLARE @sql nvarchar(max) = N'CREATE DATABASE [${u.name}]';
        EXEC (@sql);
      END;

      IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'${u.name}')
      BEGIN
        DECLARE @pwd nvarchar(256) = N'${replace(u.password, "'", "''")}';
        DECLARE @loginSql nvarchar(max) =
          N'CREATE LOGIN [${u.name}] WITH PASSWORD = N''' + @pwd + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF';
        EXEC (@loginSql);
      END;

      DECLARE @sql2 nvarchar(max) = N'
        USE [${u.name}];
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N''${u.name}'')
        BEGIN
          CREATE USER [${u.name}] FOR LOGIN [${u.name}];
          EXEC sp_addrolemember N''db_owner'', N''${u.name}'';
        END;';
      EXEC (@sql2);
    SQL
  ]

  bootstrap_sql = <<-SQL
    SET NOCOUNT ON;
    ${join("\n", local.per_user_sql)}
  SQL
}

resource "aws_security_group" "mssql" {
  name        = "${local.final_name}-sg"
  description = "Allow MSSQL inbound"
  vpc_id      = var.vpc_id

  dynamic "ingress" {
    for_each = var.allowed_cidrs
    content {
      description = "SQL Server"
      from_port   = var.port
      to_port     = var.port
      protocol    = "tcp"
      cidr_blocks = [ingress.value]
    }
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(var.tags, { Name = "${local.final_name}-sg" })
}

resource "aws_db_subnet_group" "this" {
  count       = (var.db_subnet_group_name == null && length(var.subnet_ids) >= 2) ? 1 : 0
  name        = "${local.final_name}-subnets"
  description = "DB subnet group for ${local.final_name}"
  subnet_ids  = var.subnet_ids
  tags        = merge(var.tags, { Name = "${local.final_name}-subnets" })
}

resource "aws_db_instance" "this" {
  identifier        = local.final_name
  engine            = var.engine_edition
  engine_version    = var.engine_version
  instance_class    = var.instance_class
  allocated_storage = var.allocated_storage
  storage_type      = var.storage_type
  username          = var.master_username
  password          = var.master_password
  port              = var.port

  vpc_security_group_ids = [aws_security_group.mssql.id]
  publicly_accessible    = var.publicly_accessible
  db_subnet_group_name   = var.db_subnet_group_name != null ? var.db_subnet_group_name : (length(var.subnet_ids) >= 2 ? aws_db_subnet_group.this[0].name : null)

  multi_az                   = var.multi_az
  backup_retention_period    = var.backup_retention_period
  deletion_protection        = false
  skip_final_snapshot        = true
  apply_immediately          = true
  auto_minor_version_upgrade = true
  copy_tags_to_snapshot      = true
  storage_encrypted          = var.kms_key_id != null
  kms_key_id                 = var.kms_key_id

  tags = merge(var.tags, { Name = local.final_name })
}

resource "local_file" "bootstrap" {
  count    = var.init_enable && length(var.users) > 0 ? 1 : 0
  filename = "${path.module}/bootstrap_${local.final_name}.sql"
  content  = local.bootstrap_sql
}

resource "null_resource" "bootstrap" {
  count = var.init_enable && length(var.users) > 0 ? 1 : 0

  triggers = {
    endpoint = aws_db_instance.this.address
    port     = var.port
    sql_hash = sha256(local.bootstrap_sql)
    username = var.master_username
  }

  provisioner "local-exec" {
    command = join(" ", [
      var.sql_client_cmd,
      "-S", "${aws_db_instance.this.address},${var.port}",
      "-U", var.master_username,
      "-P", "\"${var.master_password}\"",
      "-l", "30",
      "-b",
      "-i", local_file.bootstrap[0].filename
    ])
  }
}

output "endpoint" {
  value = "${aws_db_instance.this.address}:${aws_db_instance.this.port}"
}

output "identifier" {
  value = aws_db_instance.this.id
}

output "security_group_id" {
  value = aws_security_group.mssql.id
}
