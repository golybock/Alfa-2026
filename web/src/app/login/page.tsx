import { Suspense } from "react";
import BankBackdrop from "../../components/bank/BankBackdrop";
import LoggedInRedirect from "../../components/auth/LoggedInRedirect";
import LoginForm from "../../components/auth/LoginForm";
import ModalFrame from "../../components/ui/ModalFrame";

export default function LoginPage() {
  return (
    <>
      <BankBackdrop />
      <ModalFrame title="Авторизация" step={1}>
        <LoginForm />
      </ModalFrame>
      <Suspense fallback={null}>
        <LoggedInRedirect />
      </Suspense>
    </>
  );
}
