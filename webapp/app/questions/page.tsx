import { getQuestions } from "@/lib/actions/question-actions";
import QuestionsHeader from "./QuestionsHeader";
import QuestionCard from "./QuestionCard";
//import type { Question } from "@/lib/types";

// type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default async function QuestionsPage({searchParams}: {searchParams?: Promise<{tag?:string}>}) {
  // const sp = (await searchParams) ?? {};
  // const rawTag = sp.tag;
  // const tag = Array.isArray(rawTag) ? rawTag[0] : rawTag;
  // const res = await getQuestions(tag);
  // const questions: Question[] = res.data ?? [];
  // const error = res.error;
  // const params = await searchParams;
  // const {data: questions, error} = await getQuestions(params. .tag);

  // const total = questions.length;
const params = await searchParams;
const { data: questions, error } = await getQuestions(params?.tag);

if (error) throw error;

  return (
    <>
      <QuestionsHeader total={questions?.length || 0} tag={params?.tag} />
      {questions?.map((question) => (
        <div key={question.id} className="py-4 not-last:border-b w-full flex">
          <QuestionCard key={question.id} question={question} />
        </div>
      ))}
    </>
  );
}
