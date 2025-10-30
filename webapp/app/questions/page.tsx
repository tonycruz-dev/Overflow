import { getQuestions } from "@/lib/actions/question-actions";
import QuestionsHeader from "./QuestionsHeader";
import QuestionCard from "./QuestionCard";
import { QuestionParams } from "@/lib/types";
import AppPagination from "@/components/AppPagination";

export default async function QuestionsPage({ searchParams, }: { searchParams?: Promise<QuestionParams>; }) {
  const params = await searchParams;
  const { data: questions, error } = await getQuestions(params);

  if (error) throw error;

  return (
    <>
      <QuestionsHeader total={questions?.totalCount ?? 0} tag={params?.tag} />
      {questions?.items.map((question) => (
        <div key={question.id} className="py-4 not-last:border-b w-full flex">
          <QuestionCard key={question.id} question={question} />
        </div>
      ))}
      <AppPagination totalCount={questions?.totalCount ?? 0} />
    </>
  );
}
