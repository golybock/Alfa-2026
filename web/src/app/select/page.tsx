import BankBackdrop from "../../components/bank/BankBackdrop";
import CompanyRoleModal from "../../components/auth/CompanyRoleModal";
import {getSelection} from "../../lib/api/bank";

export default function SelectPage() {
	return (
		<>
			<BankBackdrop/>
			<CompanyRoleModal data={getSelection()}/>
		</>
	);
}
