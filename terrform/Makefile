STACK_DIR=infrastructure-live/eu-north-1

env-check:
	@test -f $(STACK_DIR)/.env || (echo "Missing $(STACK_DIR)/.env"; exit 1)

aws-login: env-check
	bash -lc 'set -a; source $(STACK_DIR)/.env; set +a; aws sts get-caller-identity --region eu-north-1 >/dev/null && echo "AWS OK"'

tg-init: env-check
	bash -lc 'set -a; source $(STACK_DIR)/.env; set +a; cd $(STACK_DIR) && terragrunt run-all init'

tg-apply-all: env-check
	bash -lc 'set -a; source $(STACK_DIR)/.env; set +a; cd $(STACK_DIR) && terragrunt run-all apply -auto-approve'

tg-plan-all: env-check
	bash -lc 'set -a; source $(STACK_DIR)/.env; set +a; cd $(STACK_DIR) && terragrunt run-all plan'

tg-output-all: env-check
	bash -lc 'set -a; source $(STACK_DIR)/.env; set +a; cd $(STACK_DIR) && terragrunt run-all output'

tg-destroy-all: env-check
	bash -lc 'set -a; source $(STACK_DIR)/.env; set +a; cd $(STACK_DIR) && terragrunt run-all destroy -auto-approve'
