import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { type ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../data-table";

interface TestRow {
  id: number;
  name: string;
  status: string;
}

const columns: ColumnDef<TestRow>[] = [
  { accessorKey: "id", header: "ID" },
  { accessorKey: "name", header: "Name" },
  { accessorKey: "status", header: "Status" },
];

const testData: TestRow[] = [
  { id: 1, name: "Alpha", status: "Active" },
  { id: 2, name: "Beta", status: "Inactive" },
  { id: 3, name: "Gamma", status: "Active" },
];

describe("DataTable", () => {
  it("renders column headers", () => {
    render(<DataTable columns={columns} data={testData} />);

    expect(screen.getByText("ID")).toBeInTheDocument();
    expect(screen.getByText("Name")).toBeInTheDocument();
    expect(screen.getByText("Status")).toBeInTheDocument();
  });

  it("renders row data", () => {
    render(<DataTable columns={columns} data={testData} />);

    expect(screen.getByText("Alpha")).toBeInTheDocument();
    expect(screen.getByText("Beta")).toBeInTheDocument();
    expect(screen.getByText("Gamma")).toBeInTheDocument();
  });

  it("shows default empty message when no data", () => {
    render(<DataTable columns={columns} data={[]} />);

    expect(screen.getByText("No results.")).toBeInTheDocument();
  });

  it("shows custom empty message", () => {
    render(<DataTable columns={columns} data={[]} emptyMessage="Nothing here" />);

    expect(screen.getByText("Nothing here")).toBeInTheDocument();
  });

  it("calls onRowClick when clicking a row", async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();

    render(<DataTable columns={columns} data={testData} onRowClick={onClick} />);

    await user.click(screen.getByText("Alpha"));

    expect(onClick).toHaveBeenCalledWith(testData[0]);
  });

  it("renders pagination when data exceeds page size", () => {
    const manyRows = Array.from({ length: 15 }, (_, i) => ({
      id: i + 1,
      name: `Row ${i + 1}`,
      status: "Active",
    }));

    render(<DataTable columns={columns} data={manyRows} pageSize={10} />);

    expect(screen.getByText(/Page 1 of 2/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Next" })).toBeInTheDocument();
  });

  it("does not show pagination when data fits one page", () => {
    render(<DataTable columns={columns} data={testData} pageSize={10} />);

    expect(screen.queryByText(/Page 1 of/)).not.toBeInTheDocument();
  });

  it("navigates to next page", async () => {
    const user = userEvent.setup();
    const manyRows = Array.from({ length: 15 }, (_, i) => ({
      id: i + 1,
      name: `Row ${i + 1}`,
      status: "Active",
    }));

    render(<DataTable columns={columns} data={manyRows} pageSize={10} />);

    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(screen.getByText(/Page 2 of 2/)).toBeInTheDocument();
    expect(screen.getByText("Row 11")).toBeInTheDocument();
  });
});
