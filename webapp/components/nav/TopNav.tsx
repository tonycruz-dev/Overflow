import { AcademicCapIcon } from "@heroicons/react/24/solid";
import { Button } from "@heroui/button";
import Link from "next/link";
import ThemeToggle from "./ThemeToggle";
import SearchInput from "./SearchInput";

export default function TopNav() {
  return (
    <header className="p-2 w-full fixed top-0 z-50 border-b bg-white dark:bg-black">
      <div className="flex px-10 mx-auto">
        <div className="flex items-center gap-6">
          <Link href="/" className="flex items-center gap-3 max-h-16">
            <AcademicCapIcon className="size-10 text-secondary" />
            <h3 className="text-lg font-semibold uppercase">Overflow</h3>
          </Link>
          <nav className="flex gap-3 my-2 text-medium text-neutral-500">
            <Link href="/">About</Link>
            <Link href="/">Product</Link>
            <Link href="/">Contact</Link>
          </nav>
        </div>
        <SearchInput /> 
        {/* <Input
          startContent={<MagnifyingGlassIcon className="size-6" />}
          className="ml-6"
          type="search"
          placeholder="Search..."
        /> */}
        <div className="flex basis-1/4 shrink-0 justify-end gap-3">
          <ThemeToggle />
          <Button color="secondary" variant="bordered">
            Login
          </Button>
          <Button color="secondary">Register</Button>
        </div>
      </div>
    </header>
  );
}